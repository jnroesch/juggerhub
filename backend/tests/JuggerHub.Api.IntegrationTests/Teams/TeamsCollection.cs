namespace JuggerHub.Api.IntegrationTests.Teams;

/// <summary>Shares one Testcontainers Postgres + host across all team test classes.</summary>
[CollectionDefinition("Teams")]
public sealed class TeamsCollection : ICollectionFixture<JuggerHubApiFactory>;
