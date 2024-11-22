using oed_authz.Controllers;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Models.Dto;
using oed_authz.Settings;

namespace oed_authz.UnitTests.Controllers
{
    public class AuthorizationControllerTests
    {
        private readonly IPolicyInformationPointService _fakePipService = A.Fake<IPolicyInformationPointService>();
        private readonly IProxyManagementService _fakeProxyManagementService = A.Fake<IProxyManagementService>();

        public AuthorizationControllerTests()
        {
            A.CallTo(() => _fakePipService.HandlePipRequest(A<PipRequest>._))
                .ReturnsLazily((call) =>
                {
                    var pipRequest = call.Arguments.Get<PipRequest>("pipRequest")!;
                    return Task.FromResult(new PipResponse
                    {
                        EstateSsn = pipRequest.EstateSsn,
                        RoleAssignments =
                        [
                            new PipRoleAssignment
                            {
                                EstateSsn = pipRequest.EstateSsn,
                                Id = 100,
                                RecipientSsn = "12345678901",
                                RoleCode = Constants.ProbateRoleCode,
                                Created = DateTimeOffset.UtcNow
                            },
                            new PipRoleAssignment
                            {
                                EstateSsn = pipRequest.EstateSsn,
                                Id = 100,
                                RecipientSsn = "12345678902",
                                RoleCode = Constants.FormuesfullmaktRoleCode,
                                Created = DateTimeOffset.UtcNow,
                                IsRestricted = true
                            }
                        ]
                    });
                });
        }

        [Fact]
        public async Task GetRoles_IsRestrictedAreMappepCorrectlyInResponse()
        {   
            // Arrange
            var sut = new AuthorizationController(_fakePipService, _fakeProxyManagementService);

            // Act
            var mvcResult = await sut.GetRoles(new RolesSearchRequestDto { EstateSsn = "11111111111" });

            // Assert
            mvcResult.Result.Should().BeOfType<OkObjectResult>();
            var okResult = mvcResult.Result as OkObjectResult;
            okResult?.Value.Should().NotBeNull();
            var response = okResult!.Value as RolesSearchResponseDto;
            response.Should().NotBeNull();

            response!.RoleAssignments.Should().HaveCount(2);
            response.RoleAssignments.Should().ContainSingle(ra => ra.IsRestricted == false).Which.RecipientSsn.Should().Be("12345678901");
            response.RoleAssignments.Should().ContainSingle(ra => ra.IsRestricted == true).Which.RecipientSsn.Should().Be("12345678902");
        }
    }
}
