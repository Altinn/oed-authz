using FakeItEasy;
using Microsoft.Extensions.Logging;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Services;
using oed_authz.Settings;
using oed_authz.UnitTests.TestUtils;

namespace oed_authz.UnitTests.Services.ProxyManagementServiceTests;

public class UpdateProxyRoleAssignmentsTests
{
    private readonly IRoleAssignmentsRepository
        _fakeRoleAssignmentRepository = A.Fake<IRoleAssignmentsRepository>();

    private readonly ILogger<ProxyManagementService> _fakeLogger = A.Fake<ILogger<ProxyManagementService>>();

    [Fact]
    public async Task EstateWithThreeHeirs_When_AllHeirsHaveFormuesfullmaktRole_ShouldNot_AddOrRemoveRoles()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.FormuesfulmaktRole("12345678901"),
                    factory.FormuesfulmaktRole("12345678902"),
                    factory.FormuesfulmaktRole("12345678903"),
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_ShouldNot_AddOrRemoveRoles()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.ProbateRole("12345678903"),
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task EstateWithThreeHeirs_When_AllHeirsHaveProbateOrFormuesfullmaktRoles_ShouldNot_AddOrRemoveRoles()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.FormuesfulmaktRole("12345678903"),
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_And_OneHeirHasAppointedAnotherHeirAsIndividualProxy_ShouldNot_AddOrRemoveRoles()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.ProbateRole("12345678903"),
                    factory.IndividualProxyRole(heirSsn: "12345678902", proxySsn: "12345678901")
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task EstateWithThreeHeirs_When_TwoHeirsHaveProbateRole_And_OneHeirHasAppointedTheOtherHeirAsIndividualProxy_Should_AddCollectiveProxyRole()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.FormuesfulmaktRole("12345678903"),
                    factory.IndividualProxyRole(heirSsn: "12345678902", proxySsn: "12345678901")
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(
                A<RoleAssignment>.That.Matches(ra =>
                    ra.RecipientSsn == "12345678901"
                    && ra.RoleCode == Constants.CollectiveProxyRoleCode)))
            .MustHaveHappened(1, Times.Exactly);

        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_And_TwoHeirsHaveBothAppointedTheThirdHeirAsIndividualProxy_Should_AddCollectiveProxyRole()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.ProbateRole("12345678903"),
                    factory.IndividualProxyRole(heirSsn: "12345678902", proxySsn: "12345678901"),
                    factory.IndividualProxyRole(heirSsn: "12345678903", proxySsn: "12345678901")
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(
                A<RoleAssignment>.That.Matches(ra =>
                    ra.RecipientSsn == "12345678901"
                    && ra.RoleCode == Constants.CollectiveProxyRoleCode)))
            .MustHaveHappened(1, Times.Exactly);

        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._))
            .MustNotHaveHappened();
    }


    [Fact]
    public async Task EstateWithThreeHeirs_When_OneHeirWithoutProbateRole_HasAppointedAnotherHeirAsIndividualProxy_Should_RemoveProxyRole()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.FormuesfulmaktRole("12345678903"),
                    factory.IndividualProxyRole(heirSsn: "12345678903", proxySsn: "12345678901")
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(
                A<RoleAssignment>.That.Matches(ra =>
                    ra.EstateSsn == "11111111111" &&
                    ra.RecipientSsn == "12345678901" &&
                    ra.RoleCode == Constants.IndividualProxyRoleCode &&
                    ra.HeirSsn == "12345678903")))
            .MustHaveHappened(1, Times.Exactly);

        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_And_AllHeirsHaveAppointedTheSameNonHeirAsIndividualProxy_Should_AddCollectiveProxyRole()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.ProbateRole("12345678903"),
                    factory.IndividualProxyRole(heirSsn: "12345678901", proxySsn: "98765432198"),
                    factory.IndividualProxyRole(heirSsn: "12345678902", proxySsn: "98765432198"),
                    factory.IndividualProxyRole(heirSsn: "12345678903", proxySsn: "98765432198")
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(
                A<RoleAssignment>.That.Matches(ra =>
                    ra.RecipientSsn == "98765432198"
                    && ra.RoleCode == Constants.CollectiveProxyRoleCode)))
            .MustHaveHappened(1, Times.Exactly);

        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_And_TwoHeirsHaveAppointedTheSameNonHeirAsIndividualProxy_ShouldNot_AddOrRemoveRoles()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.ProbateRole("12345678903"),
                    factory.IndividualProxyRole(heirSsn: "12345678902", proxySsn: "98765432198"),
                    factory.IndividualProxyRole(heirSsn: "12345678903", proxySsn: "98765432198")
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_And_AllHeirsHaveAppointedDifferentNonHeirAsIndividualProxy_ShouldNot_AddOrRemoveRoles()
    {
        // Arrange
        A.CallTo(() => _fakeRoleAssignmentRepository.GetRoleAssignmentsForEstate(A<string>._))
            .ReturnsLazily((call) =>
            {
                var estateSsn = call.Arguments.Get<string>("estateSsn")!;
                var factory = new RoleAssignmentFactory(estateSsn);

                return Task.FromResult(new List<RoleAssignment>
                {
                    factory.ProbateRole("12345678901"),
                    factory.ProbateRole("12345678902"),
                    factory.ProbateRole("12345678903"),
                    factory.IndividualProxyRole(heirSsn: "12345678901", proxySsn: "98765432101"),
                    factory.IndividualProxyRole(heirSsn: "12345678902", proxySsn: "98765432102"),
                    factory.IndividualProxyRole(heirSsn: "12345678903", proxySsn: "98765432103")
                });
            });

        var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

        // Act
        await sut.UpdateProxyRoleAssigments("11111111111");

        // Assert
        A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
    }
}
