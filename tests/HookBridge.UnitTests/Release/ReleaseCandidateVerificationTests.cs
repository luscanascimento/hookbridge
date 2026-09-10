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
}
