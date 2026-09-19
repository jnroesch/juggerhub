import { Component, OnInit, inject, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonDirective, CardComponent } from '../../../shared/ui';
import { PushDeviceService } from '../../../core/services/push-device.service';

/**
 * Whether the browser the member is currently using receives push notifications (feature 055).
 *
 * Sits above the preference matrix because it answers a different question: the matrix says which
 * categories would reach their devices, this says whether this device is one of them. Without it a
 * Push column switched on would read as a promise nothing can keep.
 *
 * Every state says what the member can do next. `blocked` deliberately offers no control — a site
 * that has been denied permission cannot ask again, and a button that silently does nothing is
 * worse than a sentence explaining where to change it.
 */
@Component({
  selector: 'jh-push-device-section',
  imports: [ButtonDirective, CardComponent, TranslocoPipe],
  templateUrl: './push-device-section.component.html',
  styleUrl: './push-device-section.component.css',
})
export class PushDeviceSectionComponent implements OnInit {
  private readonly push = inject(PushDeviceService);

  protected readonly state = this.push.state;
  protected readonly busy = signal(false);

  async ngOnInit(): Promise<void> {
    // Reads the current state only. Nothing here asks for permission.
    await this.push.refresh();
  }

  async enable(): Promise<void> {
    this.busy.set(true);
    try {
      await this.push.enable();
    } finally {
      this.busy.set(false);
    }
  }

  async disable(): Promise<void> {
    this.busy.set(true);
    try {
      await this.push.disable();
    } finally {
      this.busy.set(false);
    }
  }
}
