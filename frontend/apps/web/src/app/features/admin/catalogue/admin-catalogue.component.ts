import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Placeholder for the catalogue management surface (GitHub issue #24). Assigning and
 * revoking awards happens on the per-player admin detail; this page will grow the
 * create/edit/retire tooling for badge & achievement types (the 012 backend API for
 * that already exists — only this UI is pending).
 */
@Component({
  selector: 'jh-admin-catalogue',
  imports: [RouterLink],
  templateUrl: './admin-catalogue.component.html',
  styleUrl: './admin-catalogue.component.css',
})
export class AdminCatalogueComponent {}
