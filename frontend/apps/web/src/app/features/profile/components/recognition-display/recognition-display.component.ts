import { Component, computed, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { EarnedRecognition, recognitionIconUrl } from '../../../../core/models/recognition.models';

/**
 * Feature 012 US2 — displays a subject's earned badges and achievements in two grouped sections
 * (public field set only). Shared by the player profile (public + owner) and the team page. When
 * the subject has none, a single friendly empty state is shown. DESIGN.md: sentence case, mono for
 * dates, rounded tiles, no emoji.
 */
@Component({
  selector: 'jh-recognition-display',
  imports: [DatePipe],
  templateUrl: './recognition-display.component.html',
  styleUrl: './recognition-display.component.css',
})
export class RecognitionDisplayComponent {
  readonly badges = input<EarnedRecognition[]>([]);
  readonly achievements = input<EarnedRecognition[]>([]);

  protected readonly hasAny = computed(() => this.badges().length > 0 || this.achievements().length > 0);

  protected badgeIcon(id: string): string {
    return recognitionIconUrl('badge', id);
  }

  protected achievementIcon(id: string): string {
    return recognitionIconUrl('achievement', id);
  }

  protected context(item: EarnedRecognition): string | null {
    if (item.contextLabel && item.contextYear) {
      return `${item.contextLabel} · ${item.contextYear}`;
    }
    return item.contextLabel ?? (item.contextYear ? String(item.contextYear) : null);
  }
}
