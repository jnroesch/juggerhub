import { Injectable } from '@angular/core';
import { DRAFT_VERSION, EventDraft, TrainingDraft } from './wizard-draft.models';

/** Shared prefix for every wizard draft. `clearAll()` depends on it, so future drafts must adopt it. */
const PREFIX = 'jh-draft:';
const EVENT_KEY = `${PREFIX}event`;

const trainingKey = (slug: string) => `${PREFIX}training:${slug}`;

/**
 * Keeps an unfinished create-wizard alive across leaving the page (feature 045, GH #182).
 *
 * Both create wizards used to hold every answer in component memory, so navigating away, pressing
 * back, reloading, or — the reported case — having a backgrounded mobile tab discarded by the OS
 * returned the user to a blank step 1.
 *
 * **`sessionStorage`, and that is part of the contract rather than an implementation detail.** A
 * draft must not survive the tab closing (FR-010): that is what bounds the exposure of the event
 * wizard's fee recipient and account number, which are persisted by owner decision. Switching this
 * to `localStorage` would silently break that guarantee and invalidate the privacy-policy text
 * written for this feature. See specs/045-wizard-draft-persistence/contracts/wizard-draft.md.
 *
 * **Nothing here throws.** Private browsing, a full quota, storage disabled by policy and
 * hand-edited JSON all degrade to "no draft", so persistence can never become a precondition for
 * using a wizard (FR-015). The `try`/`catch` pattern follows the app's two existing storage call
 * sites — `ChunkLoadErrorHandler` and `LanguageService`.
 *
 * This class has no `HttpClient` dependency and must never acquire one: a draft never crosses the
 * network boundary (FR-014).
 */
@Injectable({ providedIn: 'root' })
export class WizardDraftStore {
  readTraining(slug: string): TrainingDraft | null {
    return this.read<TrainingDraft>(trainingKey(slug));
  }

  writeTraining(slug: string, draft: TrainingDraft): void {
    this.write(trainingKey(slug), draft);
  }

  clearTraining(slug: string): void {
    this.remove(trainingKey(slug));
  }

  readEvent(): EventDraft | null {
    return this.read<EventDraft>(EVENT_KEY);
  }

  writeEvent(draft: EventDraft): void {
    this.write(EVENT_KEY, draft);
  }

  clearEvent(): void {
    this.remove(EVENT_KEY);
  }

  /**
   * Drops every draft. Called from {@link AuthService} on sign-out and on session loss (FR-011),
   * so an unfinished wizard — which for an event includes a bank account number — is never handed
   * to whoever signs in next on a shared device.
   */
  clearAll(): void {
    const storage = this.storage();
    if (!storage) {
      return;
    }
    try {
      Object.keys(storage)
        .filter((key) => key.startsWith(PREFIX))
        .forEach((key) => storage.removeItem(key));
    } catch {
      // Storage went away mid-clear; there is nothing useful to do and nothing to report.
    }
  }

  /**
   * Reads and validates. A draft that cannot be parsed, is not an object, or carries a version
   * other than the current one is **discarded and removed** — a partially-applied draft is worse
   * than none, because the user cannot tell which of their answers survived.
   */
  private read<T>(key: string): T | null {
    const storage = this.storage();
    if (!storage) {
      return null;
    }

    let raw: string | null;
    try {
      raw = storage.getItem(key);
    } catch {
      return null;
    }
    if (raw === null) {
      return null;
    }

    let parsed: unknown;
    try {
      parsed = JSON.parse(raw);
    } catch {
      this.remove(key);
      return null;
    }

    if (
      parsed === null ||
      typeof parsed !== 'object' ||
      (parsed as { v?: unknown }).v !== DRAFT_VERSION
    ) {
      this.remove(key);
      return null;
    }

    return parsed as T;
  }

  private write(key: string, draft: unknown): void {
    const storage = this.storage();
    try {
      storage?.setItem(key, JSON.stringify(draft));
    } catch {
      // Private-mode / quota / storage disabled: the wizard still works, it just won't survive a leave.
    }
  }

  private remove(key: string): void {
    try {
      this.storage()?.removeItem(key);
    } catch {
      // As above.
    }
  }

  /** Guard against any non-browser context (SSR/tests) where storage isn't available. */
  private storage(): Storage | null {
    try {
      return typeof window !== 'undefined' ? window.sessionStorage : null;
    } catch {
      return null;
    }
  }
}
