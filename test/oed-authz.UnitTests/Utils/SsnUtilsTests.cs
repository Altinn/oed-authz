using FluentAssertions;
using oed_authz.Models;
using oed_authz.Utils;

namespace oed_authz.UnitTests.Utils;

public class SsnUtilsTests
{
    [Theory]
    [InlineData("12345678901")] // arbitrary 11 digits
    [InlineData("00000000000")] // all zeros
    [InlineData("99999999999")] // all nines
    public void IsValidSsn_WhenExactlyElevenDigits_ReturnsTrue(string ssn)
    {
        SsnUtils.IsValidSsn(ssn).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]              // empty
    [InlineData("1")]             // single digit
    [InlineData("1234567890")]    // 10 digits
    [InlineData("123456789012")]  // 12 digits
    public void IsValidSsn_WhenLengthIsNotEleven_ReturnsFalse(string ssn)
    {
        SsnUtils.IsValidSsn(ssn).Should().BeFalse();
    }

    [Theory]
    [InlineData("1234567890a")]   // trailing letter
    [InlineData("a1234567890")]   // leading letter
    [InlineData("12345 678901")]  // embedded space
    [InlineData(" 1234567890")]   // leading space
    [InlineData("1234567890 ")]   // trailing space
    [InlineData("12345-678901")]  // punctuation (also makes it 12 chars, still false)
    [InlineData("1234567890+")]   // sign character
    public void IsValidSsn_WhenContainsNonDigitCharacter_ReturnsFalse(string ssn)
    {
        SsnUtils.IsValidSsn(ssn).Should().BeFalse();
    }

    [Theory]
    [InlineData("١٢٣٤٥٦٧٨٩٠١")] // Arabic-Indic digits (11 chars, outside '0'..'9')
    [InlineData("１２３４５６７８９０１")] // full-width digits (11 chars, outside '0'..'9')
    public void IsValidSsn_WhenContainsNonAsciiDigits_ReturnsFalse(string ssn)
    {
        SsnUtils.IsValidSsn(ssn).Should().BeFalse();
    }

    [Fact]
    public void IsValidSsn_WhenNull_ReturnsFalse()
    {
        SsnUtils.IsValidSsn(null!).Should().BeFalse();
    }

    [Theory]
    [InlineData("12345678901")]              // bare SSN, returned directly
    [InlineData("/person/12345678901")]      // '/person/' prefix with leading slash
    [InlineData("person/12345678901")]       // '/person/' prefix without leading slash
    [InlineData("/person/12345678901/")]     // trailing slash removed
    [InlineData("person / 12345678901")]     // surrounding whitespace trimmed
    public void GetEstateSsnFromCloudEvent_WhenSubjectResolvesToValidSsn_ReturnsSsn(string subject)
    {
        var daEvent = new CloudEvent { Subject = subject };

        SsnUtils.GetEstateSsnFromCloudEvent(daEvent).Should().Be("12345678901");
    }

    [Theory]
    [InlineData("not-an-ssn")]                 // single non-SSN segment
    [InlineData("1234567890")]                 // 10 digits, not a valid SSN
    [InlineData("/party/12345678901")]         // wrong prefix
    [InlineData("/person/1234")]               // valid prefix but invalid SSN
    [InlineData("/person/12345678901/extra")]  // too many segments
    [InlineData("/person")]                    // missing SSN segment
    [InlineData("   ")]                         // whitespace only (not caught by null/empty guard)
    public void GetEstateSsnFromCloudEvent_WhenSubjectIsNotAValidSsnReference_ThrowsArgumentException(string subject)
    {
        var daEvent = new CloudEvent { Subject = subject };
        var act = () => SsnUtils.GetEstateSsnFromCloudEvent(daEvent);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetEstateSsnFromCloudEvent_WhenSubjectIsNull_ThrowsArgumentNullException()
    {
        var daEvent = new CloudEvent { Subject = null };
        var act = () => SsnUtils.GetEstateSsnFromCloudEvent(daEvent);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetEstateSsnFromCloudEvent_WhenSubjectIsEmpty_ThrowsArgumentException()
    {
        var daEvent = new CloudEvent { Subject = "" };
        var act = () => SsnUtils.GetEstateSsnFromCloudEvent(daEvent);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("12345678901", "123456")] // full 11-digit SSN truncated to first 6 (birth date)
    [InlineData("123456", "123456")]      // exactly 6 chars, unchanged
    [InlineData("1234567", "123456")]     // 7 chars, truncated
    public void TruncateSsn_WhenLengthIsSixOrMore_ReturnsFirstSixCharacters(string ssn, string expected)
    {
        SsnUtils.TruncateSsn(ssn).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]      // empty
    [InlineData("1")]     // single char
    [InlineData("12345")] // 5 chars, one short of the cutoff
    public void TruncateSsn_WhenLengthIsLessThanSix_ReturnsInputUnchanged(string ssn)
    {
        SsnUtils.TruncateSsn(ssn).Should().Be(ssn);
    }

    [Fact]
    public void TruncateSsn_WhenNull_ThrowsNullReferenceException()
    {
        // Documents current behavior: TruncateSsn has no null guard (unlike IsValidSsn).
        var act = () => SsnUtils.TruncateSsn(null!);

        act.Should().Throw<NullReferenceException>();
    }
}
