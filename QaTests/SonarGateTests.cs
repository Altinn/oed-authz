using Altinn.Dd.Tests.SonarGate;
using Xunit;
using Xunit.Abstractions;

namespace QaTests;

// Opt-in SonarQube quality-gate test for oed-authz. The actual runner lives in the
// Altinn.Dd.Tests.SonarGate package — this file is just the option blob. See
// https://altinn.studio/repos/digdir/dd-qa for the package source.
//
// Run with:  $env:QATESTS = "1"; dotnet test ./QaTests/QaTests.csproj
public class SonarGateTests(ITestOutputHelper output)
{
    [SkippableFact, Trait("Category", "qa")]
    public Task QualityGate_ReturnsOk() => SonarGate.RunAsync(new()
    {
        ProjectKey = "oed-authz",
        ScanCsprojRelativePath = "src/oed-authz/oed-authz.csproj",
        Coverage = new()
        {
            TestCsprojRelativePath = "test/oed-authz.UnitTests/oed-authz.UnitTests.csproj",
            // Exclude the test/runner assemblies themselves; their own (~100%-covered) code
            // would inflate the coverage % Sonar sees against new product code.
            Excludes =
            [
                "[xunit.*]*",
                "[oed-authz.UnitTests]*",
                "[oed-authz.IntegrationTests]*",
                "[QaTests]*",
            ],
        },
    }, output);
}
