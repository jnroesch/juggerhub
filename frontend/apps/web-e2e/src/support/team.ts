import { Page, expect } from '@playwright/test';
import { pickCity } from './city';

/**
 * Drive the team-creation wizard (feature 052, which replaced the single-screen form).
 *
 * The wizard is five steps — name+handle, type+city, review, logo, invite — and the team is
 * created at the **review** step, not at the end: the logo and invite steps are slug-addressed and
 * admin-gated, so they can only act on a team that already exists. That is why {@link createTeam}
 * has to keep walking after the create press to get back to where the old form left the caller.
 *
 * These live here rather than inline in each spec because there are three call sites and the walk
 * is entirely mechanical. The previous form was four lines, so it was copied; the next change to
 * the wizard should touch one file.
 */

export interface TeamWizardInput {
  name: string;
  /** The team handle. Must be unique — every caller suffixes it with a timestamp. */
  slug: string;
  /** City query for the type step. Ignored for a Mixteam, which this helper does not cover. */
  city?: string;
}

/**
 * Walk the pre-create steps and stop **on the review step**, with Create not yet pressed.
 *
 * For callers that need to control the create press itself — the resilience suite fails the POST
 * on purpose and counts the attempts, so it must not be handed a helper that assumes success.
 */
export async function fillTeamWizard(page: Page, { name, slug, city = 'Berlin' }: TeamWizardInput): Promise<void> {
  await page.goto('/teams/new');

  // 1. Name and handle. Continue is held until the availability check comes back positive, so
  //    wait for the verdict rather than relying on the click's actionability timeout to cover it.
  await page.getByTestId('team-name').fill(name);
  await page.getByTestId('team-slug').fill(slug);
  await expect(page.getByTestId('slug-ok')).toBeVisible();
  await page.getByTestId('team-next').click();

  // 2. Type and city. CityTeam is the default, but click it so the step is exercised as a person
  //    would meet it, and so a future default change does not silently make this a Mixteam.
  await page.getByTestId('type-city').click();
  await pickCity(page, 'team-city', city);
  await page.getByTestId('team-next').click();

  // 3. Review — the create press is the caller's.
  await expect(page.getByTestId('team-review')).toBeVisible();
}

/**
 * Create a team and land on its page, exactly where the pre-052 form left the caller.
 *
 * Both optional steps are skipped: this helper exists to give a test a team to work with, not to
 * exercise the logo or invite steps, which have their own coverage.
 */
export async function createTeam(page: Page, input: TeamWizardInput): Promise<void> {
  await fillTeamWizard(page, input);

  await page.getByTestId('team-create-submit').click();

  // The team now exists and the wizard is on the logo step. Skip it, then skip the invite step,
  // which is what finally navigates to the team.
  await page.getByTestId('team-logo-next').click();
  await page.getByTestId('team-finish').click();

  await expect(page).toHaveURL(new RegExp(`/t/${input.slug}`));
}
