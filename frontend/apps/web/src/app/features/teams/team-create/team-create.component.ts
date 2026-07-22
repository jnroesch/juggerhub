import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonDirective, AlertComponent } from '../../../shared/ui';
import { EMPTY, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { SlugAvailability, TeamType } from '../../../core/models/team.models';
import { TeamService } from '../../../core/services/team.service';
import { problemDetail } from '../../../core/utils/problem';

/**
 * US1 — create a team. A short form: name, a unique immutable "team address" (slug,
 * live availability like the @handle), and the type (city team with a city, or a
 * Mixteam with none). The creator becomes the first admin and lands on the team page.
 */
@Component({
  selector: 'jh-team-create',
  imports: [ReactiveFormsModule, RouterLink, ButtonDirective, AlertComponent],
  templateUrl: './team-create.component.html',
  styleUrl: './team-create.component.css',
})
export class TeamCreateComponent {
  private readonly teams = inject(TeamService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);

  protected readonly type = signal<TeamType>('CityTeam');
  protected readonly slugStatus = signal<SlugAvailability | null>(null);
  protected readonly checkingSlug = signal(false);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(50)]],
    slug: ['', [Validators.required, Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/), Validators.maxLength(30)]],
    city: ['', [Validators.maxLength(80)]],
  });

  constructor() {
    this.form.controls.slug.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((slug) => {
          this.slugStatus.set(null);
          if (!slug) {
            return EMPTY;
          }
          this.checkingSlug.set(true);
          return this.teams.checkSlug(slug);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (status) => {
          this.slugStatus.set(status);
          this.checkingSlug.set(false);
        },
        error: () => this.checkingSlug.set(false),
      });
  }

  protected setType(type: TeamType): void {
    this.type.set(type);
    const city = this.form.controls.city;
    if (type === 'Mixteam') {
      city.reset('');
      city.disable();
    } else {
      city.enable();
    }
  }

  protected onSlugInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const lowered = input.value.toLowerCase();
    if (lowered !== input.value) {
      this.form.controls.slug.setValue(lowered);
    }
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      return;
    }
    const type = this.type();
    const { name, slug, city } = this.form.getRawValue();
    if (type === 'CityTeam' && !city.trim()) {
      this.error.set('A city team needs a city.');
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.teams
      .createTeam({
        name: name.trim(),
        slug: slug.trim(),
        type,
        city: type === 'CityTeam' ? city.trim() : null,
      })
      .subscribe({
        next: (team) => this.router.navigate(['/t', team.slug]),
        error: (err) => {
          this.submitting.set(false);
          this.error.set(problemDetail(err));
        },
      });
  }
}
