using System.Text;
using FluentAssertions;
using oed_authz.Utils;

namespace oed_authz.UnitTests.Utils;

public class JwtReaderTests
{
    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Jwt(string payloadJson) => $"{Base64Url("""{"alg":"none"}""")}.{Base64Url(payloadJson)}.sig";

    [Fact]
    public void GetIssuerFromJwt_ReturnsIssuer()
    {
        JwtReader.GetIssuerFromJwt(Jwt("""{"iss":"https://test.maskinporten.no/"}"""))
            .Should().Be("https://test.maskinporten.no/");
    }

    [Theory]
    [InlineData("a.!!!.c")]                 // payload is not base64
    [InlineData("a.bm90LWpzb24.c")]         // payload is "not-json"
    public void GetIssuerFromJwt_MalformedPayload_ReturnsNull(string jwt)
    {
        JwtReader.GetIssuerFromJwt(jwt).Should().BeNull();
    }

    [Theory]
    [InlineData("""[1,2]""")]
    [InlineData("""{"iss":42}""")]
    [InlineData("""{"sub":"x"}""")]
    public void GetIssuerFromJwt_NoStringIssuer_ReturnsNull(string payloadJson)
    {
        JwtReader.GetIssuerFromJwt(Jwt(payloadJson)).Should().BeNull();
    }
}
