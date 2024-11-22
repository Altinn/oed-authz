using FakeItEasy;
using FluentAssertions;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Services;
using oed_authz.Settings;
using oed_authz.UnitTests.TestUtils;

namespace oed_authz.UnitTests.Services;

public class PipServiceTests
{
    private readonly IRoleAssignmentsRepository _fakeRepository = A.Fake<IRoleAssignmentsRepository>();

    public PipServiceTests()
    {
        A.CallTo(() => _fakeRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.FormuesfulmaktRole("12345678902"),
                });
            });

        A.CallTo(() => _fakeRepository.GetRoleAssignmentsForPerson(A<string>._, A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var recipientSsn = call.Arguments.Get<string>("recipientSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole(recipientSsn),
                });
            });
    }

    [Fact]
    public async Task HandlePipRequest_When_OnlyEstateSsnIsSpecified_Should_ReturnAllRolesFromEstate()
    {
        // Arrange
        var pipRequest = new PipRequest
        {
            EstateSsn = "11111111111",
            RecipientSsn = null
        };

        var sut = new PipService(_fakeRepository);

        // Act
        var result = await sut.HandlePipRequest(pipRequest);

        // Assert
        //result.EstateSsn.Should().Be("11111111111");
        result.RoleAssignments.Should().HaveCount(2);
        result.RoleAssignments.Should().AllSatisfy(ra => { ra.EstateSsn.Should().Be("11111111111"); });
        
        result.RoleAssignments.Should().ContainSingle(ra => 
            ra.RecipientSsn == "12345678901" &&
            ra.RoleCode == Constants.ProbateRoleCode);

        result.RoleAssignments.Should().ContainSingle(ra => 
            ra.RecipientSsn == "12345678902" &&
            ra.RoleCode == Constants.FormuesfullmaktRoleCode);
    }

    [Fact]
    public async Task HandlePipRequest_When_BothEstateSsnAndRecipientSsnAreSpecified_Should_ReturnOnlyRolesForTheGivenRecipienAndEstate()
    {
        // Arrange
        var pipRequest = new PipRequest
        {
            EstateSsn = "11111111111",
            RecipientSsn = "12345678901"
        };

        var sut = new PipService(_fakeRepository);

        // Act
        var result = await sut.HandlePipRequest(pipRequest);

        // Assert
        //result.EstateSsn.Should().Be("11111111111");
        result.RoleAssignments.Should().HaveCount(1);
        result.RoleAssignments.Should().ContainSingle(ra =>
            ra.RecipientSsn == "12345678901" &&
            ra.RoleCode == Constants.ProbateRoleCode);
    }

    [Fact]
    public async Task HandlePipRequest_When_OnlyRecipientSsnIsSpecified_Should_Throw()
    {
        // Arrange
        var pipRequest = new PipRequest
        {
            EstateSsn = null!,
            RecipientSsn = "12345678901"
        };

        var sut = new PipService(_fakeRepository);

        // Act
        var act = async () => { await sut.HandlePipRequest(pipRequest); };
        
        // Assert
        await act.Should()
            .ThrowAsync<ArgumentException>()
            .Where(e => 
                e.Message.Contains($"Invalid {nameof(PipRequest.EstateSsn)}") &&
                e.ParamName == "pipRequest");
    }

    [Fact]
    public async Task HandlePipRequest_When_InvalidRecipientSsnIsSpecified_Should_Throw()
    {
        // Arrange
        var pipRequest = new PipRequest
        {
            EstateSsn = "11111111111"!,
            RecipientSsn = "abc123"
        };

        var sut = new PipService(_fakeRepository);

        // Act
        var act = async () => { await sut.HandlePipRequest(pipRequest); };

        // Assert
        await act.Should()
            .ThrowAsync<ArgumentException>()
            .Where(e =>
                e.Message.Contains($"Invalid {nameof(PipRequest.RecipientSsn)}") &&
                e.ParamName == "pipRequest");
    }

    [Fact]
    public async Task HandlePipRequest_When_ProbateIsIssued_And_SomeHeirsDoNotHaveProbateRole_Should_ReturnResultWithIsRestricted()
    {
        // Arrange
        A.CallTo(() => _fakeRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678900"),
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.FormuesfulmaktRole("12345678903"),
                    factory.IndividualProxyRole("12345678902", "12345678900"),
                    factory.CollectiveProxyRole("98765432100"),
                });
            });

        var pipRequest = new PipRequest
        {
            EstateSsn = "11111111111"!
        };

        var sut = new PipService(_fakeRepository);

        // Act
        var result = await sut.HandlePipRequest(pipRequest);

        // Assert
        result.RoleAssignments.Should().HaveCount(6);
        result.RoleAssignments
            .Should().ContainSingle(ra => ra.IsRestricted == true)
            .Which.RecipientSsn.Should().Be("12345678903");
    }

}