namespace JuggerHub.Api.IntegrationTests.Profile;

/// <summary>Shares one Testcontainers Postgres + host across all profile test classes.</summary>
[CollectionDefinition("Profile")]
public sealed class ProfileCollection : ICollectionFixture<JuggerHubApiFactory>;
