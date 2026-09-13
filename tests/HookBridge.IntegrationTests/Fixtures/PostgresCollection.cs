namespace HookBridge.IntegrationTests.Fixtures;

[CollectionDefinition("Postgres")]
#pragma warning disable CA1711 // xUnit requires collection definitions to follow this naming convention
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
#pragma warning restore CA1711
