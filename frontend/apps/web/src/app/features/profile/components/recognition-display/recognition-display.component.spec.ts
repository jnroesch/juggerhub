import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EarnedRecognition } from '../../../../core/models/recognition.models';
import { RecognitionDisplayComponent } from './recognition-display.component';

function badge(partial: Partial<EarnedRecognition> = {}): EarnedRecognition {
  return {
    definitionId: 'b1',
    name: 'Beta Tester',
    description: 'Was here early',
    hasIcon: false,
    earnedAt: '2026-07-01T10:00:00Z',
    contextYear: null,
    contextLabel: null,
    ...partial,
  };
}

describe('RecognitionDisplayComponent', () => {
  function mount(badges: EarnedRecognition[], achievements: EarnedRecognition[]): ComponentFixture<RecognitionDisplayComponent> {
    const fixture = TestBed.createComponent(RecognitionDisplayComponent);
    fixture.componentRef.setInput('badges', badges);
    fixture.componentRef.setInput('achievements', achievements);
    fixture.detectChanges();
    return fixture;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({});
  });

  it('shows an empty state when there are no awards', () => {
    const fixture = mount([], []);
    expect(fixture.nativeElement.textContent).toContain('No badges or achievements yet.');
  });

  it('renders badges and achievements with names and context', () => {
    const fixture = mount(
      [badge({ name: 'Beta Tester' })],
      [badge({ definitionId: 'a1', name: 'Champion', contextYear: 2026, contextLabel: 'Nationals' })],
    );
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Badges');
    expect(text).toContain('Beta Tester');
    expect(text).toContain('Achievements');
    expect(text).toContain('Champion');
    expect(text).toContain('Nationals');
    expect(text).toContain('2026');
    expect(text).not.toContain('No badges or achievements yet.');
  });

  it('renders an icon image only when hasIcon is true', () => {
    const fixture = mount([badge({ definitionId: 'b9', hasIcon: true, name: 'With Icon' })], []);
    const img = fixture.nativeElement.querySelector('img') as HTMLImageElement | null;
    expect(img).not.toBeNull();
    expect(img?.getAttribute('src')).toContain('/api/v1/badges/b9/icon');
  });
});
