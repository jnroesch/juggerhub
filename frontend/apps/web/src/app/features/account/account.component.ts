import { Component } from '@angular/core';

/**
 * Guarded sample page — reachable only when authenticated (see authGuard). It
 * exists to demonstrate the route guard; unauthenticated visitors are redirected
 * toward sign-in instead of seeing this content.
 */
@Component({
  selector: 'jh-account',
  imports: [],
  templateUrl: './account.component.html',
  styleUrl: './account.component.css',
})
export class AccountComponent {}
