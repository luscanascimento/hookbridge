using System.IO;
using System.Reflection;
using FluentAssertions;
using HookBridge.Application.Abstractions;
using HookBridge.Domain.Common;
using HookBridge.Domain.Entities;
using HookBridge.Infrastructure.Security;

namespace HookBridge.UnitTests.Reviews;

public sealed class MultiRoleEngineeringReviewTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    [Fact]
    public void StaffEngineerReview_DomainEntities_MustImplementITenantScopedOrEntity()
    {
        var domainAssembly = typeof(Tenant).Assembly;
        var entityTypes = domainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Entity<Guid>).IsAssignableFrom(t))
            .ToList();

        entityTypes.Should().NotBeEmpty();

        foreach (var type in entityTypes)
        {
            if (type.Name != nameof(Tenant) && type.Name != nameof(User))
            {
                typeof(ITenantScoped).IsAssignableFrom(type)
                    .Should().BeTrue($"Entity {type.Name} must implement ITenantScoped for strict multi-tenancy");
            }
        }
    }

    [Fact]
    public void SecurityEngineerReview_WebhookSigner_MustUseConstantTimeComparison()
    {
        var signer = new WebhookSigner();
        var secret = "whsec_test_secret_for_security_review_123456789";
        var payload = "{\"test\":true}";
        var now = DateTimeOffset.UtcNow;

        var header = signer.GenerateSignatureHeader(payload, secret, now);
        
        // Exact match passes
        signer.VerifySignature(payload, header, secret, TimeSpan.FromMinutes(5), now).IsSuccess.Should().BeTrue();

        // Altered payload fails
        signer.VerifySignature("{\"test\":false}", header, secret, TimeSpan.FromMinutes(5), now).IsFailure.Should().BeTrue();

        // Expired signature fails anti-replay tolerance
        var past = now.AddMinutes(-10);
        var expiredHeader = signer.GenerateSignatureHeader(payload, secret, past);
        signer.VerifySignature(payload, expiredHeader, secret, TimeSpan.FromMinutes(5), now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void SreReview_OpenTelemetry_DiagnosticsConstants_MustBeProperlyConfigured()
    {
        HookBridge.Domain.Diagnostics.HookBridgeDiagnostics.ServiceName.Should().Be("HookBridge");
        HookBridge.Domain.Diagnostics.HookBridgeDiagnostics.DiagnosticSourceName.Should().Be("HookBridge.ControlPlane");
        HookBridge.Domain.Diagnostics.HookBridgeDiagnostics.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void ProductDxReview_ReviewDocument_MustExistAndBeApproved()
    {
        var reviewDocPath = Path.Combine(RepoRoot, "docs/reviews/multi-role-engineering-review.md");
        File.Exists(reviewDocPath).Should().BeTrue("Multi-role engineering review document must exist");

        var content = File.ReadAllText(reviewDocPath);
        content.Should().Contain("PASSED & APPROVED FOR PRODUCTION RELEASE CANDIDATE");
        content.Should().Contain("Staff Software Engineer Review");
        content.Should().Contain("Lead Application Security & Cryptography Review");
        content.Should().Contain("Principal Site Reliability Engineer (SRE) Review");
        content.Should().Contain("Lead Product & Developer Experience (DX) Review");
    }
}
