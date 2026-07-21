import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ChatService } from '../../../core/services/chat.service';
import { ChatSearchResult, PersonHit } from '../../../core/models/chat.models';
import { ChatComposeComponent } from './chat-compose.component';

function person(userId: string, handle: string, existingConversationId: string | null = null): PersonHit {
  return { userId, displayName: handle.toUpperCase(), handle, avatarUrl: null, existingConversationId };
}

function results(people: PersonHit[]): ChatSearchResult {
  return { messages: { items: [], totalCount: 0 }, people: { items: people, totalCount: people.length } };
}

const chat = { search: jest.fn(), sendDirect: jest.fn() };
let navigate: jest.SpyInstance;

function create(handle: string, state?: { userId?: string; displayName?: string }) {
  window.history.replaceState(state ?? {}, '');
  TestBed.configureTestingModule({
    providers: [{ provide: ChatService, useValue: chat }, provideRouter([])],
  });
  navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  const fixture = TestBed.createComponent(ChatComposeComponent);
  fixture.componentRef.setInput('handle', handle);
  fixture.detectChanges();
  return fixture;
}

function el(fixture: ReturnType<typeof create>, testid: string): HTMLElement | null {
  return fixture.nativeElement.querySelector(`[data-testid="${testid}"]`) as HTMLElement | null;
}

function type(fixture: ReturnType<typeof create>, text: string): void {
  // Drive the draft signal directly — the template-driven ngModel's DOM 'input' path is unreliable
  // in the zoneless test env; the send behavior (not the value accessor) is what's under test.
  (fixture.componentInstance as unknown as { draft: { set: (v: string) => void } }).draft.set(text);
  fixture.detectChanges();
}

function submit(fixture: ReturnType<typeof create>): void {
  (fixture.nativeElement.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
}

describe('ChatComposeComponent', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    chat.search.mockReturnValue(of(results([])));
    chat.sendDirect.mockReturnValue(of({ conversation: { id: 'c-new' }, message: {} }));
  });

  it('renders the recipient from navigation state without resolving or creating anything', () => {
    const fixture = create('bob', { userId: 'u-bob', displayName: 'Bob B' });
    expect(el(fixture, 'chat-compose')).not.toBeNull();
    expect(el(fixture, 'compose-recipient')?.textContent).toContain('Bob B');
    expect(chat.search).not.toHaveBeenCalled();
    expect(chat.sendDirect).not.toHaveBeenCalled();
  });

  it('resolves the target from the handle when no navigation state is present', () => {
    chat.search.mockReturnValue(of(results([person('u-bob', 'bob', null)])));
    const fixture = create('bob');
    expect(chat.search).toHaveBeenCalledWith('bob');
    expect(el(fixture, 'compose-recipient')?.textContent).toContain('BOB');
  });

  it('opens the existing conversation instead of composing when one already exists (reload)', () => {
    chat.search.mockReturnValue(of(results([person('u-bob', 'bob', 'c-1')])));
    create('bob');
    expect(navigate).toHaveBeenCalledWith(['/chat', 'c-1'], { replaceUrl: true });
  });

  it('creates the conversation on first send and navigates into it (replaceUrl)', () => {
    const fixture = create('bob', { userId: 'u-bob', displayName: 'Bob' });
    type(fixture, 'hi there');
    submit(fixture);
    expect(chat.sendDirect).toHaveBeenCalledWith('u-bob', 'hi there');
    expect(navigate).toHaveBeenCalledWith(['/chat', 'c-new'], { replaceUrl: true });
  });

  it('surfaces an error and does not navigate when the send fails', () => {
    chat.sendDirect.mockReturnValue(throwError(() => ({ error: { detail: "You can't message this player." } })));
    const fixture = create('bob', { userId: 'u-bob', displayName: 'Bob' });
    type(fixture, 'hi');
    submit(fixture);
    fixture.detectChanges();
    expect(el(fixture, 'compose-error')).not.toBeNull();
    expect(navigate).not.toHaveBeenCalled();
  });
});
