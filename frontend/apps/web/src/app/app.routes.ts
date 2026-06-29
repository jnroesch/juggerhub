import { Route } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { ShellComponent } from './layout/shell/shell.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { SignInComponent } from './features/sign-in/sign-in.component';
import { AccountComponent } from './features/account/account.component';

export const appRoutes: Route[] = [
  {
    path: '',
    component: ShellComponent,
    children: [
      { path: '', component: DashboardComponent },
      // Guarded sample route — unauthenticated access redirects toward sign-in.
      { path: 'account', component: AccountComponent, canActivate: [authGuard] },
    ],
  },
  // Sign-in is full-screen, outside the shell.
  { path: 'sign-in', component: SignInComponent },
];
