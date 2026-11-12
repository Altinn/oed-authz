using oed_authz.Controllers;
using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

        [Fact]
        public async Task AddProxy_ReturnsConflict_WhenTheProxyAlreadyExists()
        {
            // Arrange
            // This is the shape SaveChangesAsync throws on a unique violation, pinned by ProxyManagementServiceAddTests
            A.CallTo(() => _fakeProxyManagementService.Add(A<ProxyManagementRequest>._))
                .Throws(new DbUpdateException(
                    "An error occurred while saving the entity changes.",
                    new PostgresException("duplicate key value violates unique constraint", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation)));
            var sut = new AuthorizationController(_fakePipService, _fakeProxyManagementService);

            // Act
            var result = await sut.AddProxy(new ProxyAddRequestDto
            {
                Add = new ProxyAssignmentDto
                {
                    EstateSsn = "11111111111",
                    HeirSsn = "12345678901",
                    RecipientSsn = "99999999999",
                    Role = Constants.IndividualProxyRoleCode
                }
            });

            // Assert
            result.Should().BeOfType<ObjectResult>()
                .Which.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        }

        [Fact]
        public async Task RemoveProxy_ReturnsNoContent_WhenTheProxyWasRemoved()
        {
            // Arrange
            A.CallTo(() => _fakeProxyManagementService.Remove(A<ProxyManagementRequest>._)).Returns(true);
            var sut = new AuthorizationController(_fakePipService, _fakeProxyManagementService);

            // Act
            var result = await sut.RemoveProxy(CreateRemoveRequest());

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        // oed notifies the recipient when a proxy is removed, so a repeated remove must not look like a new one
        [Fact]
        public async Task RemoveProxy_ReturnsNotFound_WhenThereWasNoProxyToRemove()
        {
            // Arrange
            A.CallTo(() => _fakeProxyManagementService.Remove(A<ProxyManagementRequest>._)).Returns(false);
            var sut = new AuthorizationController(_fakePipService, _fakeProxyManagementService);

            // Act
            var result = await sut.RemoveProxy(CreateRemoveRequest());

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
            // oed tells this 404 from any other by its type, so the literal value is part of the contract
            objectResult.Value.Should().BeOfType<ProblemDetails>()
                .Which.Type.Should().Be("urn:digitaltdodsbo:problem:proxy-assignment-not-found");
        }

        private static ProxyRemoveRequestDto CreateRemoveRequest() => new()
        {
            Remove = new ProxyAssignmentDto
            {
                EstateSsn = "11111111111",
                HeirSsn = "12345678901",
                RecipientSsn = "99999999999",
                Role = Constants.IndividualProxyRoleCode
            }
        };
    }
}
