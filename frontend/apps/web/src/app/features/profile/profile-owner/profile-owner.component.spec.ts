import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { OwnerProfile } from '../../../core/models/profile.models';
import { ProfileService } from '../../../core/services/profile.service';
import { ProfileOwnerComponent } from './profile-owner.component';

const OWNER: OwnerProfile = {
  handle: 'nik-berlin',
  displayName: 'Nik',
  hometown: 'Berlin',
  description: null,
  hasAvatar: false,
  pompfen: [],
  recentActivity: [],
  teams: [],
  badges: [],
  achievements: [],
  isPublic: false,
};

/**
 * Feature 026 (US2) — the owner controls their profile visibility from the owner profile.
 * Verifies the toggle round-trips through the update payload (client is UX only; the server enforces).
 */
describe('ProfileOwnerComponent — visibility toggle (feature 026)', () => {
  let profiles: {
    getMine: jest.Mock;
    updateMine: jest.Mock;
    avatarUrl: jest.Mock;
  };

  beforeEach(() => {
    profiles = {
      getMine: jest.fn().mockReturnValue(of(OWNER)),
      updateMine: jest.fn().mockReturnValue(of({ ...OWNER, isPublic: true })),
      avatarUrl: jest.fn().mockReturnValue('/api/v1/profiles/nik-berlin/avatar'),
    };

    TestBed.configureTestingModule({
      imports: [ProfileOwnerComponent],
      providers: [provideRouter([]), { provide: ProfileService, useValue: profiles }],
    });
  });

  it('loads the current visibility into the form', () => {
    const fixture = TestBed.createComponent(ProfileOwnerComponent);
    fixture.detectChanges();
    const cmp = fixture.componentInstance as unknown as { form: { getRawValue(): { isPublic: boolean } } };
    expect(cmp.form.getRawValue().isPublic).toBe(false);
  });

  it('saves the toggled visibility in the update payload', () => {
    const fixture = TestBed.createComponent(ProfileOwnerComponent);
    fixture.detectChanges();
    const cmp = fixture.componentInstance as unknown as {
      startEdit(): void;
      save(): void;
      form: { controls: { isPublic: { setValue(v: boolean): void } } };
    };

    cmp.startEdit();
    cmp.form.controls.isPublic.setValue(true);
    cmp.save();

    expect(profiles.updateMine).toHaveBeenCalledWith(
      expect.objectContaining({ isPublic: true }),
    );
  });

  it('flips visibility instantly (without entering edit mode) and persists it', () => {
    const fixture = TestBed.createComponent(ProfileOwnerComponent);
    fixture.detectChanges();
    const cmp = fixture.componentInstance as unknown as { toggleVisibility(next: boolean): void };

    cmp.toggleVisibility(true);

    // Saves the current profile fields plus the new flag — no edit round trip.
    expect(profiles.updateMine).toHaveBeenCalledWith(
      expect.objectContaining({ isPublic: true, displayName: 'Nik' }),
    );
  });
});
