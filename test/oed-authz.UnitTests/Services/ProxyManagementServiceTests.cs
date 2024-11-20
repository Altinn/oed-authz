using FakeItEasy;
using Microsoft.Extensions.Logging;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Services;
using oed_authz.UnitTests.TestUtils;

namespace oed_authz.UnitTests.Services
{
    public class ProxyManagementServiceTests
    {
        private readonly IRoleAssignmentsRepository _fakeRoleAssignmentRepository = A.Fake<IRoleAssignmentsRepository>();
        private readonly ILogger<ProxyManagementService> _fakeLogger = A.Fake<ILogger<ProxyManagementService>>();

        [Fact]
        public async Task UpdateProxyRoleAssignments_EstateWithThreeHeirs_When_AllHeirsHaveFormuesfullmaktRole_ShouldNot_AddOrRemoveRoles()
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
            A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task UpdateProxyRoleAssignments_EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_ShouldNot_AddOrRemoveRoles()
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
            A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task UpdateProxyRoleAssignments_EstateWithThreeHeirs_When_AllHeirsHaveProbateOrFormuesfullmaktRoles_ShouldNot_AddOrRemoveRoles()
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
            A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task UpdateProxyRoleAssignments_EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_And_OneHeirHasAppointedAnotherHeirAsIndividualProxy_ShouldNot_AddOrRemoveRoles()
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
                        factory.IndividualProxyRole("12345678902", "12345678901")
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
        public async Task UpdateProxyRoleAssignments_EstateWithThreeHeirs_When_TwoHeirsHaveProbateRole_And_OneHeirHasAppointedTheOtherHeirAsIndividualProxy_Should_AddCollectiveProxyRole()
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
                        factory.IndividualProxyRole("12345678902", "12345678901")
                    });
                });

            var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

            // Act
            await sut.UpdateProxyRoleAssigments("11111111111");

            // Assert
            A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustHaveHappened();
            A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task UpdateProxyRoleAssignments_EstateWithThreeHeirs_When_AllHeirsHaveProbateRole_And_TwoHeirsHaveBothAppointedTheThirdHeirAsIndividualProxy_Should_AddCollectiveProxyRole()
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
                        factory.IndividualProxyRole("12345678902", "12345678901"),
                        factory.IndividualProxyRole("12345678903", "12345678901")
                    });
                });

            var sut = new ProxyManagementService(_fakeRoleAssignmentRepository, _fakeLogger);

            // Act
            await sut.UpdateProxyRoleAssigments("11111111111");

            // Assert
            A.CallTo(() => _fakeRoleAssignmentRepository.AddRoleAssignment(A<RoleAssignment>._)).MustHaveHappened();
            A.CallTo(() => _fakeRoleAssignmentRepository.RemoveRoleAssignment(A<RoleAssignment>._)).MustNotHaveHappened();
        }
    }
}
