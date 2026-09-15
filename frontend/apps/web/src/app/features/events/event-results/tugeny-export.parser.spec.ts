import { parseTugenyRankingExport } from './tugeny-export.parser';

describe('parseTugenyRankingExport (contracts/tugeny.md §2)', () => {
  const ok = (text: string) => {
    const result = parseTugenyRankingExport(text);
    if (!result.ok) {
      throw new Error(`expected ${text} to parse`);
    }
    return result.rows;
  };
  const refused = (text: string) => expect(parseTugenyRankingExport(text)).toEqual({ ok: false });

  it('reads compact JSON', () => {
    expect(ok('[{"name":"Seven Sins","position":1},{"name":"Basilisken","position":2}]')).toEqual([
      { position: 1, name: 'Seven Sins' },
      { position: 2, name: 'Basilisken' },
    ]);
  });

  it('reads Qt-style indented JSON with sorted keys', () => {
    const text = `[
    {
        "name": "Rigor Mortis",
        "position": 1
    },
    {
        "name": "Eclipse",
        "position": 2
    }
]
`;
    expect(ok(text).map((r) => r.name)).toEqual(['Rigor Mortis', 'Eclipse']);
  });

  it('accepts a position written as digits', () => {
    expect(ok('[{"position":"3","name":"Schatten"}]')).toEqual([{ position: 3, name: 'Schatten' }]);
  });

  it('keeps ties and orders by position, then by the order in the text', () => {
    const rows = ok(
      '[{"name":"C","position":3},{"name":"A","position":1},{"name":"B2","position":2},{"name":"B1","position":2}]',
    );
    expect(rows).toEqual([
      { position: 1, name: 'A' },
      { position: 2, name: 'B2' },
      { position: 2, name: 'B1' },
      { position: 3, name: 'C' },
    ]);
  });

  it('ignores keys it does not know and trims names', () => {
    expect(ok('[{"name":"  Pink Pain  ","position":1,"team_id":7}]')).toEqual([{ position: 1, name: 'Pink Pain' }]);
  });

  it.each([
    ['plain text', 'hello'],
    ['an object', '{}'],
    ['an empty array', '[]'],
    ['a row that is not an object', '[1]'],
    ['a missing name', '[{"position":1}]'],
    ['a missing position', '[{"name":"A"}]'],
    ['an empty name', '[{"name":"   ","position":1}]'],
    ['a name longer than 80 characters', `[{"name":"${'x'.repeat(81)}","position":1}]`],
    ['position 0', '[{"name":"A","position":0}]'],
    ['a negative position', '[{"name":"A","position":-1}]'],
    ['a fractional position', '[{"name":"A","position":1.5}]'],
    ['a non-numeric position', '[{"name":"A","position":"x"}]'],
    ['duplicate names, ignoring case and spaces', '[{"name":"Eclipse","position":1},{"name":" eclipse ","position":2}]'],
  ])('refuses %s', (_label, text) => refused(text));

  it('refuses more than 128 rows', () => {
    const rows = Array.from({ length: 129 }, (_, i) => ({ name: `Team ${i}`, position: i + 1 }));
    refused(JSON.stringify(rows));
  });
});
