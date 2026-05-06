using Altinn.Dd.InternalEvents.Estate;
using FluentAssertions;
using oed_authz.Services;

namespace oed_authz.UnitTests.Services;

public class EstateCaseUpdatedEventExtensionsTests
{
    private static EstateCaseUpdatedEvent MakeEvent() => new()
    {
        Time = DateTimeOffset.UtcNow,
        CaseId = "case-id",
        CaseNumber = "abc123",
        CaseStatus = CaseStatus.Mottatt,
        DistrictCourtName = "Oslo tingrett",
        ProbateDeadline = DateTimeOffset.UtcNow.AddDays(60),
        HeirRolesV2 = []
    };

    #region IsFeilfort

    [Fact]
    public void IsFeilfort_WhenCaseStatusIsFeilfort_ReturnsTrue()
    {
        var evt = MakeEvent();
        evt.CaseStatus = CaseStatus.Feilfort;
        evt.IsFeilfort().Should().BeTrue();
    }

    [Theory]
    [InlineData(CaseStatus.Mottatt)]
    [InlineData(CaseStatus.Ferdigbehandlet)]
    [InlineData(CaseStatus.OverfortAnnenDomstol)]
    public void IsFeilfort_WhenCaseStatusIsNotFeilfort_ReturnsFalse(string caseStatus)
    {
        var evt = MakeEvent();
        evt.CaseStatus = caseStatus;
        evt.IsFeilfort().Should().BeFalse();
    }

    [Fact]
    public void IsFeilfort_WhenCaseStatusIsNull_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.CaseStatus = null!;
        evt.IsFeilfort().Should().BeFalse();
    }

    #endregion

    #region IsProbateIssued

    [Fact]
    public void IsProbateIssued_WhenProbateResultV2HasResult_ReturnsTrue()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2 { Result = "ISSUED" };
        evt.IsProbateIssued().Should().BeTrue();
    }

    [Fact]
    public void IsProbateIssued_WhenResultTypeIsSet_ReturnsTrue()
    {
        var evt = MakeEvent();
        evt.ResultType = "PROBATE";
        evt.IsProbateIssued().Should().BeTrue();
    }

    [Fact]
    public void IsProbateIssued_WhenBothProbateResultV2ResultAndResultTypeAreSet_ReturnsTrue()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2 { Result = "ISSUED" };
        evt.ResultType = "PROBATE";
        evt.IsProbateIssued().Should().BeTrue();
    }

    [Fact]
    public void IsProbateIssued_WhenProbateResultV2IsNullAndResultTypeIsNull_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.IsProbateIssued().Should().BeFalse();
    }

    [Fact]
    public void IsProbateIssued_WhenProbateResultV2ResultIsEmptyAndResultTypeIsNull_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2 { Result = "" };
        evt.IsProbateIssued().Should().BeFalse();
    }

    [Fact]
    public void IsProbateIssued_WhenProbateResultV2IsNullAndResultTypeIsEmpty_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.ResultType = "";
        evt.IsProbateIssued().Should().BeFalse();
    }

    #endregion

    #region ContainsProbateHeirs

    [Fact]
    public void ContainsProbateHeirs_WhenProbateResultV2HasHeirs_ReturnsTrue()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2
        {
            Result = "ISSUED",
            Heirs = [new PersonProbateHeir { Nin = "12345678901" }]
        };
        evt.ContainsProbateHeirs().Should().BeTrue();
    }

    [Fact]
    public void ContainsProbateHeirs_WhenProbateResultV2IsNull_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.ContainsProbateHeirs().Should().BeFalse();
    }

    [Fact]
    public void ContainsProbateHeirs_WhenProbateResultV2HeirsIsNull_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2 { Result = "ISSUED", Heirs = null! };
        evt.ContainsProbateHeirs().Should().BeFalse();
    }

    [Fact]
    public void ContainsProbateHeirs_WhenProbateResultV2HeirsIsEmpty_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2 { Result = "ISSUED", Heirs = [] };
        evt.ContainsProbateHeirs().Should().BeFalse();
    }

    #endregion

    #region HasIncompleteRoleInformation

    [Fact]
    public void HasIncompleteRoleInformation_WhenProbateIssuedAndHeirsListIsEmpty_ReturnsTrue()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2 { Result = "ISSUED", Heirs = [] };
        evt.HasIncompleteRoleInformation().Should().BeTrue();
    }

    [Fact]
    public void HasIncompleteRoleInformation_WhenResultTypeSetAndNoHeirs_ReturnsTrue()
    {
        var evt = MakeEvent();
        evt.ResultType = "PROBATE";
        evt.HasIncompleteRoleInformation().Should().BeTrue();
    }

    [Fact]
    public void HasIncompleteRoleInformation_WhenProbateIssuedAndHasHeirs_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2
        {
            Result = "ISSUED",
            Heirs = [new PersonProbateHeir { Nin = "12345678901" }]
        };
        evt.HasIncompleteRoleInformation().Should().BeFalse();
    }

    [Fact]
    public void HasIncompleteRoleInformation_WhenNeitherProbateResultV2NorResultTypeIsSet_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.HasIncompleteRoleInformation().Should().BeFalse();
    }

    [Fact]
    public void HasIncompleteRoleInformation_WhenHeirsPresentButProbateNotIssued_ReturnsFalse()
    {
        var evt = MakeEvent();
        evt.ProbateResultV2 = new ProbateResultV2
        {
            Result = "",
            Heirs = [new PersonProbateHeir { Nin = "12345678901" }]
        };
        evt.HasIncompleteRoleInformation().Should().BeFalse();
    }

    #endregion
}
