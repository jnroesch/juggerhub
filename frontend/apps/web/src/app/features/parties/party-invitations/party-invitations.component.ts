import { Component, OnInit, inject, signal } from '@angular/core';
import { ButtonDirective, EmptyStateComponent } from '../../../shared/ui';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PartyInvitableUser, PartyInvitation, PartyInviteLink } from '../../../core/models/party.models';
import { PartyService } from '../../../core/services/party.service';

/**
 * Party co-admin invitations (feature 016 · wireframe 6d). Team-scoped: a shareable link plus a
 * member search that invites teammates directly. Mirrors the event co-admin screen.
 */
@Component({
  selector: 'jh-party-invitations',
  imports: [RouterLink, FormsModule, ButtonDirective, EmptyStateComponent],
  templateUrl: './party-invitations.component.html',
  styleUrl: './party-invitations.component.css',
})
export class PartyInvitationsComponent implements OnInit {
  private readonly parties = inject(PartyService);
  private readonly route = inject(ActivatedRoute);

  protected readonly link = signal<PartyInviteLink | null>(null);
  protected readonly pending = signal<PartyInvitation[]>([]);
  protected readonly results = signal<PartyInvitableUser[]>([]);
  protected query = '';
  protected id = '';

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') ?? '';
    this.parties.getInviteLink(this.id).subscribe({ next: (l) => this.link.set(l), error: () => undefined });
    this.loadPending();
  }

  private loadPending(): void {
    this.parties.listInvites(this.id).subscribe({ next: (p) => this.pending.set(p.items), error: () => undefined });
  }

  protected rotateLink(): void {
    this.parties.rotateInviteLink(this.id).subscribe({ next: (l) => this.link.set(l), error: () => undefined });
  }

  protected search(): void {
    const q = this.query.trim();
    if (q.length === 0) {
      this.results.set([]);
      return;
    }
    this.parties.searchMembers(this.id, q).subscribe({ next: (p) => this.results.set(p.items), error: () => undefined });
  }

  protected invite(user: PartyInvitableUser): void {
    this.parties.createInvite(this.id, { userId: user.userId }).subscribe({
      next: () => {
        this.loadPending();
        this.search();
      },
      error: () => undefined,
    });
  }

  protected revoke(invitationId: string): void {
    this.parties.revokeInvite(this.id, invitationId).subscribe({ next: () => this.loadPending(), error: () => undefined });
  }
}
