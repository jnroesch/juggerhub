import { HttpErrorResponse } from '@angular/common/http';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { translocoTestingModule } from '../../../testing/transloco-testing';
import { NewsPostComponent, NewsPostRemoval } from './news-post.component';
import { NewsPostEditing } from './news-post-editing';

interface Post {
  id: string;
  body: string;
}

/** A page listing posts the way the four news surfaces do: it owns the list, the controls own the rest. */
@Component({
  imports: [NewsPostComponent],
  providers: [NewsPostEditing],
  template: `
    <ul>
      @for (p of posts(); track p.id) {
        <li>
          <jh-news-post
            [postId]="p.id"
            [body]="p.body"
            [canManage]="canManage()"
            [maxLength]="maxLength()"
            editHint="news.editHint.party"
            deleteBody="news.deleteBody.party"
            [save]="save"
            [remove]="remove"
            (saved)="onSaved($event)"
            (removed)="onRemoved(p.id, $event)"
          >
            <p data-testid="display">{{ p.body }}</p>
          </jh-news-post>
        </li>
      }
    </ul>
  `,
})
class HostComponent {
  readonly posts = signal<Post[]>([
    { id: 'p1', body: 'Meet 07:00 at the Aral.' },
    { id: 'p2', body: 'Bring water.' },
  ]);
  readonly canManage = signal(true);
  readonly maxLength = signal(1000);
  readonly save = jest.fn<Observable<Post>, [string, string]>();
  readonly remove = jest.fn<Observable<unknown>, [string]>();
  readonly removals: (NewsPostRemoval & { id: string })[] = [];

  onSaved(post: Post): void {
    this.posts.update((list) => list.map((p) => (p.id === post.id ? post : p)));
  }

  onRemoved(id: string, removal: NewsPostRemoval): void {
    this.removals.push({ id, ...removal });
    this.posts.update((list) => list.filter((p) => p.id !== id));
  }
}

/**
 * Feature 059 — the edit and delete controls every news surface shares (built for team news in 057).
 * The team page's own spec pins the same behaviour through a real page; these pin the component's
 * contract with any host: what it asks the host to do, and what it tells the host happened.
 */
describe('NewsPostComponent', () => {
  let fixture: ComponentFixture<HostComponent>;
  let host: HostComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HostComponent, translocoTestingModule()] });
    fixture = TestBed.createComponent(HostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
  });

  function el<T extends HTMLElement = HTMLElement>(selector: string): T | null {
    return fixture.nativeElement.querySelector(selector);
  }

  function click(selector: string): void {
    const target = el(selector);
    if (!target) {
      throw new Error(`Nothing matches ${selector}`);
    }
    target.click();
    fixture.detectChanges();
  }

  function openEditor(id: string): HTMLTextAreaElement {
    click(`[data-news-menu-trigger="${id}"]`);
    click('[data-testid="news-edit"]');
    const input = el<HTMLTextAreaElement>('[data-testid="news-edit-input"]');
    if (!input) {
      throw new Error('The editor did not open');
    }
    return input;
  }

  function type(input: HTMLTextAreaElement, text: string): void {
    input.value = text;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function openDeleteDialog(id: string): void {
    click(`[data-news-menu-trigger="${id}"]`);
    click('[data-testid="news-delete"]');
  }

  const trigger = (id: string) => el<HTMLButtonElement>(`[data-news-menu-trigger="${id}"]`);
  const displays = () =>
    Array.from(fixture.nativeElement.querySelectorAll('[data-testid="display"]')).map((p) => (p as HTMLElement).textContent?.trim());

  it('offers no controls to someone who may not manage the post', () => {
    host.canManage.set(false);
    fixture.detectChanges();

    expect(el('[data-news-menu-trigger]')).toBeNull();
    expect(displays()).toEqual(['Meet 07:00 at the Aral.', 'Bring water.']);
  });

  it('saves the trimmed text through the host, hands back the result and returns focus to the menu button', () => {
    host.save.mockReturnValue(of({ id: 'p1', body: 'Meet 07:00 at the other Aral.' }));
    const input = openEditor('p1');
    expect(document.activeElement).toBe(input);
    expect(input.value).toBe('Meet 07:00 at the Aral.');

    type(input, '  Meet 07:00 at the other Aral.  ');
    click('[data-testid="news-edit-save"]');

    expect(host.save).toHaveBeenCalledTimes(1);
    expect(host.save).toHaveBeenCalledWith('p1', 'Meet 07:00 at the other Aral.');
    expect(el('[data-testid="news-editor"]')).toBeNull();
    expect(displays()[0]).toBe('Meet 07:00 at the other Aral.');
    expect(document.activeElement).toBe(trigger('p1'));
  });

  it('sends nothing for a cancel or for unchanged text', () => {
    click(`[data-news-menu-trigger="p1"]`);
    click('[data-testid="news-edit"]');
    click('[data-testid="news-edit-cancel"]');
    expect(el('[data-testid="news-editor"]')).toBeNull();
    expect(document.activeElement).toBe(trigger('p1'));

    const input = openEditor('p1');
    type(input, ' Meet 07:00 at the Aral. ');
    click('[data-testid="news-edit-save"]');

    expect(host.save).not.toHaveBeenCalled();
    expect(el('[data-testid="news-editor"]')).toBeNull();
  });

  it('hides the post while editing it, and keeps it', () => {
    openEditor('p1');

    const display = el('[data-testid="display"]');
    expect(display).not.toBeNull();
    expect(display?.parentElement?.hidden).toBe(true);
  });

  it('keeps the editor and the typed text when a save fails, and says so in its own words', () => {
    host.save.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503, error: { detail: 'Server English.' } })));
    const input = openEditor('p1');
    type(input, 'Changed.');

    click('[data-testid="news-edit-save"]');

    expect(el<HTMLTextAreaElement>('[data-testid="news-edit-input"]')?.value).toBe('Changed.');
    expect(el('[data-testid="news-edit-error"]')?.textContent?.trim()).toBe("We couldn't save your changes. Try again.");
    expect(fixture.nativeElement.textContent).not.toContain('Server English.');
  });

  it('tells the host the post is gone when a save answers 404', () => {
    host.save.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    const input = openEditor('p1');
    type(input, 'Changed.');

    click('[data-testid="news-edit-save"]');

    expect(host.removals).toEqual([{ id: 'p1', gone: true }]);
    expect(displays()).toEqual(['Bring water.']);
    // The lock went with the editor: the remaining post's menu opens.
    expect(trigger('p2')?.disabled).toBe(false);
  });

  it('asks before deleting, with the safe answer focused, and Keep changes nothing', () => {
    openDeleteDialog('p1');

    const dialog = el('[data-testid="confirm-dialog"]');
    expect(dialog?.getAttribute('aria-modal')).toBe('true');
    expect(dialog?.textContent).toContain("It disappears for the whole crew, together with its alerts.");
    expect(document.activeElement).toBe(el('[data-testid="confirm-dialog-keep"]'));

    click('[data-testid="confirm-dialog-keep"]');

    expect(el('[data-testid="confirm-dialog"]')).toBeNull();
    expect(host.remove).not.toHaveBeenCalled();
    expect(document.activeElement).toBe(trigger('p1'));
  });

  it('deletes through the host on confirm and tells it the post went', () => {
    host.remove.mockReturnValue(of(undefined));
    openDeleteDialog('p1');

    click('[data-testid="confirm-dialog-confirm"]');

    expect(host.remove).toHaveBeenCalledWith('p1');
    expect(host.removals).toEqual([{ id: 'p1', gone: false }]);
    expect(el('[data-testid="confirm-dialog"]')).toBeNull();
    expect(displays()).toEqual(['Bring water.']);
  });

  it('keeps the dialog when a delete fails, and reports a 404 as already gone', () => {
    host.remove.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 503 })));
    openDeleteDialog('p1');

    click('[data-testid="confirm-dialog-confirm"]');

    expect(el('[data-testid="confirm-dialog"]')).not.toBeNull();
    expect(el('[data-testid="confirm-dialog-error"]')?.textContent?.trim()).toBe("We couldn't delete the post. Try again.");
    expect(host.removals).toEqual([]);

    host.remove.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 404 })));
    click('[data-testid="confirm-dialog-confirm"]');

    expect(host.removals).toEqual([{ id: 'p1', gone: true }]);
    expect(el('[data-testid="confirm-dialog"]')).toBeNull();
  });

  it('locks every menu while a post is being edited, and releases them after', () => {
    openEditor('p1');

    expect(trigger('p1')?.disabled).toBe(true);
    expect(trigger('p2')?.disabled).toBe(true);

    click('[data-testid="news-edit-cancel"]');

    expect(trigger('p2')?.disabled).toBe(false);
  });

  it('keeps an open editor and its text when the page rebuilds its list, and locks nothing while the post is away', () => {
    type(openEditor('p1'), 'Half-typed');
    const both = host.posts();

    host.posts.set([both[1]]);
    fixture.detectChanges();
    // The post being edited is not on screen: the menu that is there opens.
    expect(trigger('p2')?.disabled).toBe(false);

    host.posts.set(both);
    fixture.detectChanges();

    expect(el<HTMLTextAreaElement>('[data-testid="news-edit-input"]')?.value).toBe('Half-typed');
    expect(trigger('p2')?.disabled).toBe(true);
  });

  it('keeps one menu open at a time', () => {
    click(`[data-news-menu-trigger="p1"]`);
    expect(trigger('p1')?.getAttribute('aria-expanded')).toBe('true');

    click(`[data-news-menu-trigger="p2"]`);

    expect(fixture.nativeElement.querySelectorAll('[data-testid="news-menu"]').length).toBe(1);
    expect(trigger('p1')?.getAttribute('aria-expanded')).toBe('false');
    expect(trigger('p2')?.getAttribute('aria-expanded')).toBe('true');
  });

  it('closes an open menu on Escape and hands focus back to its button', () => {
    click(`[data-news-menu-trigger="p2"]`);

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();

    expect(el('[data-testid="news-menu"]')).toBeNull();
    expect(document.activeElement).toBe(trigger('p2'));
  });

  it('caps the editor at the limit of its kind of news and shows its hint', () => {
    host.maxLength.set(2000);
    fixture.detectChanges();

    openEditor('p1');

    expect(el('[data-testid="news-edit-input"]')?.getAttribute('maxlength')).toBe('2000');
    expect(el('[data-testid="news-editor"]')?.textContent).toContain("Saving won't notify the crew again.");
  });
});
