import { Route } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { onboardingGuard } from './core/guards/onboarding.guard';
import { ShellComponent } from './layout/shell/shell.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { AccountComponent } from './features/account/account.component';
import { SignInComponent } from './features/auth/sign-in/sign-in.component';
import { RegisterComponent } from './features/auth/register/register.component';
import { ForgotPasswordComponent } from './features/auth/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './features/auth/reset-password/reset-password.component';
import { VerifyEmailComponent } from './features/auth/verify-email/verify-email.component';
import { ProfileOwnerComponent } from './features/profile/profile-owner/profile-owner.component';
import { ProfilePublicComponent } from './features/profile/profile-public/profile-public.component';
import { OnboardingComponent } from './features/onboarding/onboarding.component';

export const appRoutes: Route[] = [
  {
    path: '',
    component: ShellComponent,
    children: [
      { path: '', component: DashboardComponent },
      // Guarded sample route — unauthenticated access redirects toward sign-in.
      { path: 'account', component: AccountComponent, canActivate: [authGuard] },
      // Owner profile view/edit lives inside the shell, behind the auth guard.
      { path: 'profile', component: ProfileOwnerComponent, canActivate: [authGuard] },
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
];
