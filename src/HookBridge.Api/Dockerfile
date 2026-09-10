# Stage 1: Base Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Stage 2: Build & Restore
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files for optimal layer caching
COPY ["src/HookBridge.Domain/HookBridge.Domain.csproj", "src/HookBridge.Domain/"]
COPY ["src/HookBridge.Application/HookBridge.Application.csproj", "src/HookBridge.Application/"]
COPY ["src/HookBridge.Infrastructure/HookBridge.Infrastructure.csproj", "src/HookBridge.Infrastructure/"]
COPY ["src/HookBridge.Api/HookBridge.Api.csproj", "src/HookBridge.Api/"]

RUN dotnet restore "src/HookBridge.Api/HookBridge.Api.csproj"

# Copy remaining source code and build
COPY ["src/", "src/"]
WORKDIR "/src/src/HookBridge.Api"
RUN dotnet build "HookBridge.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Stage 3: Publish
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "HookBridge.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Stage 4: Final Image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HookBridge.Api.dll"]
