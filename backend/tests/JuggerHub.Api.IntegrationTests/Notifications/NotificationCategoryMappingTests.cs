using JuggerHub.Entities;

namespace JuggerHub.Api.IntegrationTests.Notifications;

/// <summary>
/// Every <see cref="NotificationType"/> maps to the category its settings row claims to govern.
///
/// This is a pure mapping test — no fixture, no container — because it guards a failure that never
/// throws: <see cref="NotificationCategories.For"/> ends in <c>_ =&gt; NotificationCategory.TeamNews</c>,
/// so a type added without a case compiles, passes every other test, and is silently filed under the
/// recipient's *Team news* toggle. Someone who muted team news would then stop receiving, say,
/// cancellation notices for events they signed up for, and nothing anywhere would report it.
///
/// The exhaustive case below is the point: it fails when a new type is added without a mapping.
/// </summary>
public sealed class NotificationCategoryMappingTests
{
    [Theory]
    [InlineData(NotificationType.TeamInvite, NotificationCategory.InvitesAndRoster)]
    [InlineData(NotificationType.TeamRoleChanged, NotificationCategory.InvitesAndRoster)]
    [InlineData(NotificationType.PartyRequest, NotificationCategory.InvitesAndRoster)]
    [InlineData(NotificationType.MarketInvite, NotificationCategory.InvitesAndRoster)]
    [InlineData(NotificationType.TeamNews, NotificationCategory.TeamNews)]
    [InlineData(NotificationType.PartyNews, NotificationCategory.TeamNews)]
    [InlineData(NotificationType.TrainingScheduled, NotificationCategory.Trainings)]
    [InlineData(NotificationType.TrainingUpdated, NotificationCategory.Trainings)]
    [InlineData(NotificationType.EventCancelled, NotificationCategory.Events)]
    public void Type_maps_to_its_category(NotificationType type, NotificationCategory expected) =>
        Assert.Equal(expected, NotificationCategories.For(type));

    [Fact]
    public void Every_notification_type_has_an_explicit_mapping()
    {
        // The InlineData set above must cover the whole enum. If a new type is added without a
        // case in NotificationCategories.For, the default arm hides it — so assert coverage here
        // rather than trusting the switch to complain.
        var covered = new[]
        {
            NotificationType.TeamInvite,
            NotificationType.TeamRoleChanged,
            NotificationType.PartyRequest,
            NotificationType.MarketInvite,
            NotificationType.TeamNews,
            NotificationType.PartyNews,
            NotificationType.TrainingScheduled,
            NotificationType.TrainingUpdated,
            NotificationType.EventCancelled,
        };

        var all = Enum.GetValues<NotificationType>();
        Assert.Equal(all.Length, covered.Distinct().Count());
        Assert.Empty(all.Except(covered));
    }
}
