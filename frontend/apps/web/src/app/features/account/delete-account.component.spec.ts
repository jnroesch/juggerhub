import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { DeleteAccountComponent } from './delete-account.component';
import { AccountDeletionService } from '../../core/services/account-deletion.service';
import { AuthService } from '../../core/services/auth.service';
import { AccountDeletionPreview } from '../../core/models/account-deletion.models';
import { translocoTestingModule } from '../../../testing/transloco-testing';

/**
 * The danger zone on /account (feature 037).
 *
 * These specs guard the three things a member can actually be hurt by: confirming without meaning
 * to, being told only half of what will happen, and being refused one obligation at a time.
 */
describe('DeleteAccountComponent', () => {
  let fixture: ComponentFixture<DeleteAccountComponent>;
  let api: jasmine.SpyObj<AccountDeletionService> | { preview: jest.Mock; deleteAccount: jest.Mock };

  const clean: AccountDeletionPreview = {
    canDelete: true,
    blockers: [],
    retained: ['ChatMessages', 'NewsPosts'],
    erased: ['Profile', 'Photo'],
  };

  const blocked: AccountDeletionPreview = {
    canDelete: false,
    blockers: [
      { kind: 'SoleTeamAdmin', subjectId: 'a', subjectName: 'Rheinfeuer', remedy: 'MakeAnotherAdmin' },
      { kind: 'SoleTeamAdmin', subjectId: 'b', subjectName: 'Nordwind', remedy: 'MakeAnotherAdmin' },
    ],
    retained: [],
    erased: [],
  };

  function build(preview: AccountDeletionPreview) {
    api = {
      preview: jest.fn().mockReturnValue(of(preview)),
      deleteAccount: jest.fn().mockReturnValue(of(void 0)),
    };

    TestBed.configureTestingModule({
      imports: [translocoTestingModule()],
      providers: [
        provideRouter([]),
        { provide: AccountDeletionService, useValue: api },
        { provide: AuthService, useValue: { clearSession: jest.fn() } },
      ],
    });

    fixture = TestBed.createComponent(DeleteAccountComponent);
    fixture.detectChanges();
  }

  function click(testId: string) {
    fixture.nativeElement.querySelector(`[data-testid="${testId}"]`)?.click();
    fixture.detectChanges();
  }

  function el(testId: string): HTMLElement | null {
    return fixture.nativeElement.querySelector(`[data-testid="${testId}"]`);
  }

  it('does not call the API until the member opens the danger zone', () => {
    build(clean);
    expect(api.preview).not.toHaveBeenCalled();
    expect(el('delete-account-panel')).toBeNull();
  });

  it('re-fetches every time it opens, so a resolved blocker is not served from cache', () => {
    build(clean);

    click('delete-account-open');
    click('delete-account-cancel');
    click('delete-account-open');

    expect(api.preview).toHaveBeenCalledTimes(2);
  });

  it('names every blocker at once rather than one refusal at a time', () => {
    build(blocked);
    click('delete-account-open');

    const text = el('delete-account-blockers')?.textContent ?? '';
    expect(text).toContain('Rheinfeuer');
    expect(text).toContain('Nordwind');

    // And there is nothing to press while blocked.
    expect(el('delete-account-confirm')).toBeNull();
  });

  it('refuses to submit until both the password and the exact confirmation word are given', () => {
    build(clean);
    click('delete-account-open');

    const confirm = el('delete-account-confirm') as HTMLButtonElement;
    expect(confirm.disabled).toBe(true);

    fixture.componentInstance['password'].set('hunter2');
    fixture.detectChanges();
    expect((el('delete-account-confirm') as HTMLButtonElement).disabled).toBe(true);

    fixture.componentInstance['confirmation'].set('nearly');
    fixture.detectChanges();
    expect((el('delete-account-confirm') as HTMLButtonElement).disabled).toBe(true);

    fixture.componentInstance['confirmation'].set('DELETE');
    fixture.detectChanges();
    expect((el('delete-account-confirm') as HTMLButtonElement).disabled).toBe(false);
  });

  it('cancelling leaves no typed password behind', () => {
    build(clean);
    click('delete-account-open');

    fixture.componentInstance['password'].set('hunter2');
    click('delete-account-cancel');

    expect(fixture.componentInstance['password']()).toBe('');
    expect(api.deleteAccount).not.toHaveBeenCalled();
  });

  it('shows the blockers the server found when a 409 arrives mid-confirmation', () => {
    build(clean);
    click('delete-account-open');

    api.deleteAccount.mockReturnValue(
      throwError(() => ({
        status: 409,
        error: {
          blockers: [
            { kind: 'SoleTeamAdmin', subjectId: 'c', subjectName: 'Südsturm', remedy: 'MakeAnotherAdmin' },
          ],
        },
      })),
    );

    fixture.componentInstance['password'].set('hunter2');
    fixture.componentInstance['confirmation'].set('DELETE');
    fixture.detectChanges();
    click('delete-account-confirm');

    // Nothing was deleted, and the newly-discovered obligation is on screen.
    expect(el('delete-account-blockers')?.textContent).toContain('Südsturm');
  });
});
