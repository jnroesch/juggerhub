import { Route } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { onboardingGuard } from './core/guards/onboarding.guard';
import { ShellComponent } from './layout/shell/shell.component';
import { AccountComponent } from './features/account/account.component';
import { SignInComponent } from './features/auth/sign-in/sign-in.component';
import { RegisterComponent } from './features/auth/register/register.component';
import { ForgotPasswordComponent } from './features/auth/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './features/auth/reset-password/reset-password.component';
import { VerifyEmailComponent } from './features/auth/verify-email/verify-email.component';
import { ProfileOwnerComponent } from './features/profile/profile-owner/profile-owner.component';
import { ProfilePublicComponent } from './features/profile/profile-public/profile-public.component';
import { OnboardingComponent } from './features/onboarding/onboarding.component';
import { TeamCreateComponent } from './features/teams/team-create/team-create.component';
import { TeamDetailComponent } from './features/teams/team-detail/team-detail.component';
import { TeamInvitationsComponent } from './features/teams/team-invitations/team-invitations.component';
import { TeamSettingsComponent } from './features/teams/team-settings/team-settings.component';
import { InviteAcceptComponent } from './features/teams/invite-accept/invite-accept.component';
// Events (feature 006) are lazy-loaded to keep them out of the initial bundle.

export const appRoutes: Route[] = [
  {
    path: '',
    component: ShellComponent,
    children: [
      {
        path: '',
        pathMatch: 'full',
        canActivate: [authGuard],
        loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      // Alerts / notifications (feature 008) — placeholder screen; the system arrives later.
      {
        path: 'alerts',
        canActivate: [authGuard],
        loadComponent: () => import('./features/alerts/alerts.component').then((m) => m.AlertsComponent),
      },
      // "My team" chooser (feature 008) — for players on more than one team.
      {
        path: 'my-team',
        canActivate: [authGuard],
        loadComponent: () => import('./features/my-team/my-team.component').then((m) => m.MyTeamComponent),
      },
      // Home "see all" lists (feature 008) — full upcoming events + full news feed.
      {
        path: 'up-next',
        canActivate: [authGuard],
        loadComponent: () => import('./features/dashboard/see-all/up-next-list.component').then((m) => m.UpNextListComponent),
      },
      {
        path: 'news',
        canActivate: [authGuard],
        loadComponent: () => import('./features/dashboard/see-all/news-page.component').then((m) => m.NewsPageComponent),
      },
      // Guarded sample route — unauthenticated access redirects toward sign-in.
      { path: 'account', component: AccountComponent, canActivate: [authGuard] },
      // Notification settings (feature 011) — the per-category × per-channel matrix. Lazy-loaded.
      {
        path: 'settings/notifications',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/settings/notifications/notification-settings.component').then(
            (m) => m.NotificationSettingsComponent,
          ),
      },
      // Owner profile view/edit lives inside the shell, behind the auth guard.
      { path: 'profile', component: ProfileOwnerComponent, canActivate: [authGuard] },
      // Teams (feature 005) — create + the members-only team space, in the shell.
      { path: 'teams/new', component: TeamCreateComponent, canActivate: [authGuard] },
      // Public team page (feature 009) — anonymous-viewable; members/admins see more inline.
      { path: 't/:slug', component: TeamDetailComponent },
      { path: 't/:slug/invitations', component: TeamInvitationsComponent, canActivate: [authGuard] },
      { path: 't/:slug/settings', component: TeamSettingsComponent, canActivate: [authGuard] },
      // Events (feature 006) — create is authed; the event page itself is public. Lazy-loaded.
      {
        path: 'events/new',
        canActivate: [authGuard],
        loadComponent: () => import('./features/events/event-create/event-create.component').then((m) => m.EventCreateComponent),
      },
      {
        path: 'events/:id',
        loadComponent: () => import('./features/events/event-detail/event-detail.component').then((m) => m.EventDetailComponent),
      },
      {
        path: 'events/:id/manage',
        canActivate: [authGuard],
        loadComponent: () => import('./features/events/event-manage/event-manage.component').then((m) => m.EventManageComponent),
      },
      {
        path: 'events/:id/edit',
        canActivate: [authGuard],
        loadComponent: () => import('./features/events/event-edit/event-edit.component').then((m) => m.EventEditComponent),
      },
      {
        path: 'events/:id/contacts',
        canActivate: [authGuard],
        loadComponent: () => import('./features/events/event-contacts/event-contacts.component').then((m) => m.EventContactsComponent),
      },
      {
        path: 'events/:id/admins',
        canActivate: [authGuard],
        loadComponent: () => import('./features/events/event-admins/event-admins.component').then((m) => m.EventAdminsComponent),
      },
      // Event parties (feature 016) — form from an event; the crew is managed under /parties/:id.
      {
        path: 'events/:id/enter-party',
        canActivate: [authGuard],
        loadComponent: () => import('./features/parties/party-create/party-create.component').then((m) => m.PartyCreateComponent),
      },
      {
        path: 'parties/:id',
        canActivate: [authGuard],
        loadComponent: () => import('./features/parties/party-manage/party-manage.component').then((m) => m.PartyManageComponent),
      },
      {
        path: 'parties/:id/news',
        canActivate: [authGuard],
        loadComponent: () => import('./features/parties/party-news/party-news.component').then((m) => m.PartyNewsComponent),
      },
      {
        path: 'parties/:id/invitations',
        canActivate: [authGuard],
        loadComponent: () => import('./features/parties/party-invitations/party-invitations.component').then((m) => m.PartyInvitationsComponent),
      },
      {
        // Event marketplace (feature 017) — recruiting toggle + applications/invites + direct invite.
        path: 'parties/:id/recruiting',
        canActivate: [authGuard],
        loadComponent: () => import('./features/marketplace/recruiting/recruiting.component').then((m) => m.RecruitingComponent),
      },
      // Trainings (feature 018) — team-scoped tab + create, session page + attendance.
      {
        path: 't/:slug/trainings',
        canActivate: [authGuard],
        loadComponent: () => import('./features/trainings/trainings-tab/trainings-tab.component').then((m) => m.TrainingsTabComponent),
      },
      {
        path: 't/:slug/trainings/new',
        canActivate: [authGuard],
        loadComponent: () => import('./features/trainings/training-create/training-create.component').then((m) => m.TrainingCreateComponent),
      },
      {
        // The public-shareable session entry — any signed-in user (outsiders join public sessions as guests).
        path: 'trainings/sessions/:id',
        canActivate: [authGuard],
        loadComponent: () => import('./features/trainings/training-session/training-session.component').then((m) => m.TrainingSessionComponent),
      },
      {
        path: 'trainings/sessions/:id/edit',
        canActivate: [authGuard],
        loadComponent: () => import('./features/trainings/training-edit/training-edit.component').then((m) => m.TrainingEditComponent),
      },
      {
        path: 'trainings/sessions/:id/attendance',
        canActivate: [authGuard],
        loadComponent: () => import('./features/trainings/attendance/attendance.component').then((m) => m.AttendanceComponent),
      },
      // Browse / search (feature 007) — anonymous (no guard), in the shell, lazy-loaded.
      { path: 'browse', pathMatch: 'full', redirectTo: 'browse/teams' },
      {
        path: 'browse/teams',
        loadComponent: () => import('./features/browse/browse-teams/browse-teams.component').then((m) => m.BrowseTeamsComponent),
      },
      {
        path: 'browse/events',
        loadComponent: () => import('./features/browse/browse-events/browse-events.component').then((m) => m.BrowseEventsComponent),
      },
      {
        path: 'browse/players',
        loadComponent: () => import('./features/browse/browse-players/browse-players.component').then((m) => m.BrowsePlayersComponent),
      },
    ],
  },
  // Auth screens are full-screen, outside the shell.
  { path: 'sign-in', component: SignInComponent },
  { path: 'register', component: RegisterComponent },
  { path: 'forgot-password', component: ForgotPasswordComponent },
  { path: 'reset-password', component: ResetPasswordComponent },
  { path: 'verify-email', component: VerifyEmailComponent },
  // First-login onboarding — full-screen, outside the shell. authGuard requires a
  // session; onboardingGuard bounces already-onboarded users to the dashboard.
  { path: 'onboarding', component: OnboardingComponent, canActivate: [authGuard, onboardingGuard] },
  // Public, unauthenticated share page — full-screen, outside the shell.
  { path: 'u/:handle', component: ProfilePublicComponent },
  // Admin area (feature 013) — full-screen shell with its own shield header and nav;
  // gated to platform admins (server-enforced; adminGuard is UX only). Lazy-loaded.
  // Children: overview (landing) · users (search/list) · users/:handle (player detail)
  // · catalogue (the feature-012 badge/achievement management surface, re-mounted).
  {
    path: 'admin',
    canActivate: [authGuard, adminGuard],
    loadComponent: () =>
      import('./features/admin/shell/admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/admin/overview/admin-overview.component').then((m) => m.AdminOverviewComponent),
      },
      {
        path: 'users',
        loadComponent: () =>
          import('./features/admin/users/admin-users.component').then((m) => m.AdminUsersComponent),
      },
      {
        path: 'users/:handle',
        loadComponent: () =>
          import('./features/admin/user-detail/admin-user-detail.component').then((m) => m.AdminUserDetailComponent),
      },
      {
        path: 'teams',
        loadComponent: () =>
          import('./features/admin/teams/admin-teams.component').then((m) => m.AdminTeamsComponent),
      },
      {
        path: 'teams/:slug',
        loadComponent: () =>
          import('./features/admin/team-detail/admin-team-detail.component').then((m) => m.AdminTeamDetailComponent),
      },
      {
        path: 'catalogue',
        loadComponent: () =>
          import('./features/admin/catalogue/admin-catalogue.component').then((m) => m.AdminCatalogueComponent),
      },
    ],
  },
  // Invite accept — full-screen, outside the shell; preview is anonymous, accept needs auth.
  { path: 'join/:slug/:token', component: InviteAcceptComponent },
  // Event co-admin invite accept — full-screen, outside the shell; preview anonymous, accept needs auth.
  {
    path: 'event-invite/:token',
    loadComponent: () => import('./features/events/event-invite-accept/event-invite-accept.component').then((m) => m.EventInviteAcceptComponent),
  },
  // Party co-admin invite accept (feature 016) — full-screen, outside the shell.
  {
    path: 'party-invite/:token',
    loadComponent: () => import('./features/parties/party-invite-accept/party-invite-accept.component').then((m) => m.PartyInviteAcceptComponent),
  },
];
