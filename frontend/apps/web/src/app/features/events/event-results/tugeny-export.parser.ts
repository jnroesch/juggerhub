/**
 * Reads the text Tugeny's desktop app produces from *Export → Export Ranking for JTR*
 * (feature 050, contracts/tugeny.md §2). Pure: no network, no DOM.
 *
 * The shape was read from Tugeny's own binary (research R1): a JSON array of objects carrying
 * `position` and `name`. What the binary cannot show — whether `position` is written as a number
 * or a string, key order, indentation — is tolerated. Anything else is refused as one friendly
 * error, never guessed at: a wrong ranking is worse than no ranking.
 *
 * Every parsed row starts unconnected. Deciding which JuggerHub team a name means is a person's
 * choice (FR-011), and the server re-validates whatever is saved.
 */

export interface ParsedRankingRow {
  position: number;
  name: string;
}

export type TugenyExportParseResult = { ok: true; rows: ParsedRankingRow[] } | { ok: false };

/** The server's caps (TournamentResultService): at most 128 placements, names up to 80 characters. */
export const MAX_EXPORT_ROWS = 128;
export const MAX_TEAM_NAME = 80;
const MAX_POSITION = 999;

const FAIL: TugenyExportParseResult = { ok: false };

export function parseTugenyRankingExport(text: string): TugenyExportParseResult {
  let data: unknown;
  try {
    data = JSON.parse(text);
  } catch {
    return FAIL;
  }

  if (!Array.isArray(data) || data.length === 0 || data.length > MAX_EXPORT_ROWS) {
    return FAIL;
  }

  const rows: (ParsedRankingRow & { index: number })[] = [];
  const seen = new Set<string>();

  for (const [index, item] of data.entries()) {
    if (item === null || typeof item !== 'object' || Array.isArray(item)) {
      return FAIL;
    }

    const record = item as Record<string, unknown>;
    const name = typeof record['name'] === 'string' ? record['name'].trim() : null;
    const position = readPosition(record['position']);
    if (!name || name.length > MAX_TEAM_NAME || position === null) {
      return FAIL;
    }

    // Tugeny refuses two teams with the same name, so a duplicate means this is not its export.
    const key = name.toLocaleLowerCase();
    if (seen.has(key)) {
      return FAIL;
    }
    seen.add(key);

    rows.push({ position, name, index });
  }

  rows.sort((a, b) => a.position - b.position || a.index - b.index);
  return { ok: true, rows: rows.map(({ position, name }) => ({ position, name })) };
}

/** A positive whole number, written as a number or as digits; null for anything else. */
function readPosition(value: unknown): number | null {
  let n: number;
  if (typeof value === 'number') {
    n = value;
  } else if (typeof value === 'string' && /^\s*\d+\s*$/.test(value)) {
    n = Number(value);
  } else {
    return null;
  }

  return Number.isInteger(n) && n >= 1 && n <= MAX_POSITION ? n : null;
}
