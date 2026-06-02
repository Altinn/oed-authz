using FakeItEasy;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using oed_authz.Controllers;
using oed_authz.Interfaces;
using oed_authz.Models;
using oed_authz.Models.Dto;
using oed_authz.Settings;

namespace oed_authz.UnitTests.Controllers
{
    public class PipControllerTests
    {
        private readonly IPolicyInformationPointService _fakePipService = A.Fake<IPolicyInformationPointService>();
        private readonly IWebHostEnvironment _fakeEnvironment = A.Fake<IWebHostEnvironment>();
        private readonly ILogger<PipController> _fakeLogger = A.Fake<ILogger<PipController>>();

        public PipControllerTests()
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
                            }
                        ]
                    });
                });
        }

        [Fact]
        public async Task HandlePipRequest_InProduction_DoesNotLog()
        {
            // Arrange
            _fakeEnvironment.EnvironmentName = Environments.Production;
            var sut = new PipController(_fakePipService, _fakeEnvironment, _fakeLogger);

            // Act
            var mvcResult = await sut.HandlePipRequest(new PipRequestDto { From = "11111111111", To = "12345678901" });

            // Assert
            mvcResult.Result.Should().BeOfType<OkObjectResult>();
            AssertLogCount(LogLevel.Information, expectedCalls: 0);
        }

        [Theory]
        [InlineData("Development")]
        [InlineData("Staging")]
        [InlineData("Testing")]
        public async Task HandlePipRequest_OutsideProduction_LogsDebugInfo(string environmentName)
        {
            // Arrange
            _fakeEnvironment.EnvironmentName = environmentName;
            var sut = new PipController(_fakePipService, _fakeEnvironment, _fakeLogger);

            // Act
            var mvcResult = await sut.HandlePipRequest(new PipRequestDto { From = "11111111111", To = "12345678901" });

            // Assert
            mvcResult.Result.Should().BeOfType<OkObjectResult>();
            AssertLogCount(LogLevel.Information, expectedCalls: 1);
        }

        private void AssertLogCount(LogLevel level, int expectedCalls)
        {
            // ILogger.LogInformation is an extension method; FakeItEasy can only intercept
            // the underlying Log<TState>() call.
            A.CallTo(_fakeLogger)
                .Where(call =>
                    call.Method.Name == nameof(ILogger.Log) &&
                    call.GetArgument<LogLevel>(0) == level)
                .MustHaveHappened(expectedCalls, Times.Exactly);
        }
    }
}
