using System.IO;
using FluentAssertions;

namespace HookBridge.UnitTests.Deployment;

public sealed class CiCdWorkflowTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    [Fact]
    public void CiWorkflow_ShouldExistAndHaveRequiredJobs()
    {
        var ciPath = Path.Combine(RepoRoot, ".github/workflows/ci.yml");
        File.Exists(ciPath).Should().BeTrue("CI workflow file must exist");

        var content = File.ReadAllText(ciPath);
        content.Should().Contain("name: Continuous Integration");
        content.Should().Contain("backend-ci:");
        content.Should().Contain("frontend-ci:");
        content.Should().Contain("docker-ci:");
        content.Should().Contain("actions/setup-dotnet@v4");
        content.Should().Contain("actions/setup-node@v4");
        content.Should().Contain("dotnet test HookBridge.sln");
        content.Should().Contain("npm run build -- --configuration production");
    }

    [Fact]
    public void SecurityAuditWorkflow_ShouldExistAndHaveAuditJobs()
    {
        var auditPath = Path.Combine(RepoRoot, ".github/workflows/security-audit.yml");
        File.Exists(auditPath).Should().BeTrue("Security audit workflow must exist");

        var content = File.ReadAllText(auditPath);
        content.Should().Contain("name: Security & Vulnerability Audit");
        content.Should().Contain("dotnet-audit:");
        content.Should().Contain("npm-audit:");
        content.Should().Contain("gitleaks-scan:");
        content.Should().Contain("dotnet list package --vulnerable");
        content.Should().Contain("npm audit");
    }

    [Fact]
    public void ReleaseWorkflow_ShouldExistAndPublishGhcrImages()
    {
        var releasePath = Path.Combine(RepoRoot, ".github/workflows/release.yml");
        File.Exists(releasePath).Should().BeTrue("Release workflow must exist");

        var content = File.ReadAllText(releasePath);
        content.Should().Contain("name: Release & Container Publishing");
        content.Should().Contain("publish-images:");
        content.Should().Contain("ghcr.io");
        content.Should().Contain("docker/build-push-action@v5");
        content.Should().Contain("softprops/action-gh-release@v2");
    }

    [Fact]
    public void EditorConfig_ShouldExistAndEnforceRules()
    {
        var editorConfigPath = Path.Combine(RepoRoot, ".editorconfig");
        File.Exists(editorConfigPath).Should().BeTrue("Master .editorconfig must exist");

        var content = File.ReadAllText(editorConfigPath);
        content.Should().Contain("root = true");
        content.Should().Contain("indent_style = space");
        content.Should().Contain("[*.cs]");
        content.Should().Contain("[*.{ts,js}]");
        content.Should().Contain("dotnet_diagnostic.CA3075.severity = error");
    }
}
