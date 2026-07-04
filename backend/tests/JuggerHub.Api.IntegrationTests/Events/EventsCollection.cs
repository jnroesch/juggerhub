namespace JuggerHub.Api.IntegrationTests.Events;

/// <summary>Shares one Testcontainers Postgres + host across all event test classes.</summary>
[CollectionDefinition("Events")]
public sealed class EventsCollection : ICollectionFixture<JuggerHubApiFactory>;
