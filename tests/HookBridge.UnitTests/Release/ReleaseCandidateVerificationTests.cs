using System.IO;
using System.Reflection;
using FluentAssertions;
using HookBridge.Domain.Common;

namespace HookBridge.UnitTests.Release;

public sealed class ReleaseCandidateVerificationTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    [Fact]
    public void ReleaseCandidate_AssemblyVersion_ShouldBeOneZeroZero()
    {
        var domainAssembly = typeof(Entity<>).Assembly;
        var version = domainAssembly.GetName().Version;
        
        version.Should().NotBeNull();
        version!.Major.Should().Be(1);
        version.Minor.Should().Be(0);
        version.Build.Should().Be(0);
    }

    [Fact]
    public void ReleaseCandidate_DocumentationArtifacts_MustBePresent()
    {
        File.Exists(Path.Combine(RepoRoot, "README.md")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "CHANGELOG.md")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "RELEASE_NOTES.md")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "docs/ROADMAP_PROGRESS.md")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "docs/PROJECT_CONTEXT.md")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "docker-compose.yml")).Should().BeTrue();
        File.Exists(Path.Combine(RepoRoot, "Dockerfile")).Should().BeTrue();
    }

    [Fact]
    public void ReleaseCandidate_FrontendPackageJson_ShouldHaveVersion100()
    {
        var packageJsonPath = Path.Combine(RepoRoot, "src/HookBridge.Web/package.json");
        File.Exists(packageJsonPath).Should().BeTrue();

        var content = File.ReadAllText(packageJsonPath);
        content.Should().Contain("\"version\": \"1.0.0\"");
    }

    [Fact]
    public void ReleaseCandidate_Roadmap_AllPhasesMustBeCompleted()
    {
        var roadmapPath = Path.Combine(RepoRoot, "docs/ROADMAP_PROGRESS.md");
        File.Exists(roadmapPath).Should().BeTrue();

        var content = File.ReadAllText(roadmapPath);
        content.Should().NotContain("⬜ Pending", "All roadmap phases must be completed for Release Candidate");
    }

    [Fact]
    public void ReleaseCandidate_DirectoryBuildProps_ContainsExpectedMetadata()
    {
        var propsPath = Path.Combine(RepoRoot, "Directory.Build.props");
        File.Exists(propsPath).Should().BeTrue("Directory.Build.props must exist");

        var content = File.ReadAllText(propsPath);
        content.Should().Contain("<Version>1.0.0</Version>");
        content.Should().Contain("<InformationalVersion>1.0.0-rc.1</InformationalVersion>");
        content.Should().Contain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
        content.Should().Contain("<TargetFramework>net9.0</TargetFramework>");
        content.Should().Contain("luscanascimento/hookbridge");
    }

    [Fact]
    public void ReleaseCandidate_ProductionCompose_AllSecretsParameterized()
    {
        var prodComposePath = Path.Combine(RepoRoot, "docker-compose.prod.yml");
        File.Exists(prodComposePath).Should().BeTrue("docker-compose.prod.yml must exist");

        var content = File.ReadAllText(prodComposePath);
        content.Should().Contain("${DB_PASSWORD}");
        content.Should().Contain("${JWT_SECRET_KEY}");
        content.Should().Contain("${EVENTFLOW_API_KEY}");
        content.Should().Contain("${WEBHOOK_ENCRYPTION_MASTER_KEY}");
        content.Should().Contain("${RABBITMQ_PASSWORD}");
        content.Should().Contain("${REDIS_PASSWORD}");
    }

    [Fact]
    public void ReleaseCandidate_ReleaseNotesAndChangelog_Synchronized()
    {
        var releaseNotesPath = Path.Combine(RepoRoot, "RELEASE_NOTES.md");
        var changelogPath = Path.Combine(RepoRoot, "CHANGELOG.md");

        File.Exists(releaseNotesPath).Should().BeTrue();
        File.Exists(changelogPath).Should().BeTrue();

        var notes = File.ReadAllText(releaseNotesPath);
        var log = File.ReadAllText(changelogPath);

        notes.Should().Contain("v1.0.0-rc.1");
        log.Should().Contain("[1.0.0-rc.1]");
        notes.Should().Contain("HookBridge Release Candidate 1");
    }

    [Fact]
    public void ReleaseCandidate_HardeningCampaign_AllPhasesCompleted()
    {
        var contextPath = Path.Combine(RepoRoot, "docs/PROJECT_CONTEXT.md");
        File.Exists(contextPath).Should().BeTrue();

        var content = File.ReadAllText(contextPath);
        content.Should().Contain("FASE 1 a FASE 12 Concluídas");
        content.Should().NotContain("⏳ Próxima");
        content.Should().NotContain("⏳ Planejada");
    }
}
