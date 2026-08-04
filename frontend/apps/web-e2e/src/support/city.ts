import { APIRequestContext, Page, expect } from '@playwright/test';

/**
 * Select a city in a `jh-city-picker` (feature 030 replaced the freeform hometown/city text inputs
 * with a type-ahead combobox). `containerTestId` is the element wrapping the picker — e.g.
 * `onboarding-city`, `profile-hometown`, `team-city`. Types the query, waits for the backend-backed
 * suggestions, clicks the first (or a matching) option, and asserts the confirmed-selection chip.
 */
export async function pickCity(
  page: Page,
  containerTestId: string,
  query: string,
  choice?: string | RegExp,
): Promise<void> {
  const container = page.getByTestId(containerTestId);
  const input = container.getByTestId('city-picker-input');
  await input.click();
  await input.fill(query);
  const option = choice
    ? container.getByRole('option', { name: choice })
    : container.getByRole('option').first();
  await option.click();
  await expect(container.getByTestId('city-picker-chip')).toBeVisible();
}

/**
 * Clear the city an edit form loaded with. A picker holding a selection renders the confirmed chip
 * INSTEAD of the search input, so `pickCity` alone can never reach a picker that is already set —
 * changing a city is always clear-then-search.
 */
export async function clearCity(page: Page, containerTestId: string): Promise<void> {
  const container = page.getByTestId(containerTestId);
  await container.getByTestId('city-picker-chip').getByRole('button', { name: 'Clear city' }).click();
  await expect(container.getByTestId('city-picker-input')).toBeVisible();
}

/**
 * Resolve a real city externalId via the backend search, for API-level fixtures that must send a
 * structured `LocationSelection` instead of the old freeform `city` string. Uses the caller's
 * session-bound request context (city search is authenticated-only since feature 026).
 */
export async function resolveCityExternalId(
  request: APIRequestContext,
  query = 'Berlin',
): Promise<string> {
  const res = await request.get('/api/v1/cities/search', { params: { q: query } });
  if (!res.ok()) {
    throw new Error(`City search failed (${res.status()}) for "${query}".`);
  }
  const list = (await res.json()) as { externalId: string }[];
  if (list.length === 0) {
    throw new Error(`No city match for "${query}".`);
  }
  return list[0].externalId;
}
