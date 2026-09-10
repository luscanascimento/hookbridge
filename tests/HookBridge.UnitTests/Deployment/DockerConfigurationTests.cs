using System.IO;
using FluentAssertions;

namespace HookBridge.UnitTests.Deployment;

public sealed class DockerConfigurationTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

    [Fact]
    public void BackendDockerfile_ShouldExistAndHaveValidMultiStageConfiguration()
    {
        var dockerfilePath = Path.Combine(RepoRoot, "Dockerfile");
        File.Exists(dockerfilePath).Should().BeTrue("Root Dockerfile for Backend API must exist");

        var content = File.ReadAllText(dockerfilePath);
        content.Should().Contain("FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base");
        content.Should().Contain("FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build");
        content.Should().Contain("FROM build AS publish");
        content.Should().Contain("FROM base AS final");
        content.Should().Contain("USER app");
        content.Should().Contain("EXPOSE 8080");
        content.Should().Contain("ENTRYPOINT [\"dotnet\", \"HookBridge.Api.dll\"]");
    }

    [Fact]
    public void FrontendDockerfile_ShouldExistAndHaveValidMultiStageConfiguration()
    {
        var webDockerfilePath = Path.Combine(RepoRoot, "src/HookBridge.Web/Dockerfile");
        File.Exists(webDockerfilePath).Should().BeTrue("Frontend Dockerfile must exist");

        var content = File.ReadAllText(webDockerfilePath);
        content.Should().Contain("FROM node:22-alpine AS build");
        content.Should().Contain("FROM nginx:1.27-alpine AS final");
        content.Should().Contain("COPY --from=build /app/dist/hookbridge-web/browser .");
        content.Should().Contain("EXPOSE 80");
        content.Should().Contain("HEALTHCHECK");
    }

    [Fact]
    public void NginxConfig_ShouldContainSecurityHeadersAndReverseProxyRules()
    {
        var nginxPath = Path.Combine(RepoRoot, "src/HookBridge.Web/nginx.conf");
        File.Exists(nginxPath).Should().BeTrue("nginx.conf must exist for frontend container");

        var content = File.ReadAllText(nginxPath);
        content.Should().Contain("add_header X-Frame-Options \"DENY\" always;");
        content.Should().Contain("add_header X-Content-Type-Options \"nosniff\" always;");
        content.Should().Contain("add_header Content-Security-Policy");
        content.Should().Contain("location /api/");
        content.Should().Contain("location /hubs/");
        content.Should().Contain("location /health/");
        content.Should().Contain("proxy_set_header Upgrade $http_upgrade;");
        content.Should().Contain("try_files $uri $uri/ /index.html;");
    }

    [Fact]
    public void DockerCompose_ShouldDefineAllEssentialInfrastructureServices()
    {
        var composePath = Path.Combine(RepoRoot, "docker-compose.yml");
        File.Exists(composePath).Should().BeTrue("docker-compose.yml must exist");

        var content = File.ReadAllText(composePath);
        content.Should().Contain("hookbridge-api:");
        content.Should().Contain("hookbridge-web:");
        content.Should().Contain("postgres:");
        content.Should().Contain("rabbitmq:");
        content.Should().Contain("redis:");
        content.Should().Contain("jaeger:");
        content.Should().Contain("postgres-data:");
        content.Should().Contain("rabbitmq-data:");
        content.Should().Contain("hookbridge-net:");
    }

    [Fact]
    public void EnvExample_ShouldDocumentAllSecurityAndInfrastructureSecrets()
    {
        var envPath = Path.Combine(RepoRoot, ".env.example");
        File.Exists(envPath).Should().BeTrue(".env.example must exist");

        var content = File.ReadAllText(envPath);
        content.Should().Contain("DB_PASSWORD");
        content.Should().Contain("JWT_SECRET_KEY");
        content.Should().Contain("EVENTFLOW_API_KEY");
        content.Should().Contain("RABBITMQ_PASSWORD");
        content.Should().Contain("REDIS_PASSWORD");
        content.Should().Contain("OTEL_EXPORTER_OTLP_ENDPOINT");
    }
}
