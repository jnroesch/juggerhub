import { Route } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { SignInComponent } from './features/sign-in/sign-in.component';
import { AccountComponent } from './features/account/account.component';

export const appRoutes: Route[] = [
  { path: '', component: DashboardComponent },
  { path: 'sign-in', component: SignInComponent },
  // Guarded sample route — unauthenticated access redirects toward sign-in.
  { path: 'account', component: AccountComponent, canActivate: [authGuard] },
];
