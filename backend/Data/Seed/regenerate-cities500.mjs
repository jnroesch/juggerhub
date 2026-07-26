// Joins GeoNames cities500 + countryInfo + admin1 into one compact gzipped seed file for the backend.
// Regenerate with: node prep-seed.mjs   (downloads cities500/countryInfo/admin1 first if missing)
import { readFileSync, createReadStream, createWriteStream } from 'fs';
import { createInterface } from 'readline';
import { createGzip } from 'zlib';

const DIR = new URL('.', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');

// cc(ISO2) -> country name
const country = new Map();
for (const line of readFileSync(DIR + 'countryInfo.txt', 'utf8').split('\n')) {
  if (!line || line.startsWith('#')) continue;
  const c = line.split('\t');
  if (c[0] && c[4]) country.set(c[0], c[4]);
}

// "CC.admin1" -> region name
const region = new Map();
for (const line of readFileSync(DIR + 'admin1CodesASCII.txt', 'utf8').split('\n')) {
  if (!line) continue;
  const c = line.split('\t');
  if (c[0] && c[1]) region.set(c[0], c[1]);
}

const gz = createGzip({ level: 9 });
const out = createWriteStream(DIR + 'cities500.seed.tsv.gz');
gz.pipe(out);

const rl = createInterface({ input: createReadStream(DIR + 'cities500.txt', 'utf8'), crlfDelay: Infinity });
let n = 0;
const clean = (s) => (s || '').replace(/[\t\r\n]/g, ' ').trim();
// Keep only Latin-script alternate names (drop CJK/Cyrillic/Arabic bloat) to keep the seed small
// while preserving English/local exonyms that matter for search (Munich, Cologne, Wien…).
const latinAlts = (s) => (s || '').split(',').map((a) => a.trim())
  .filter((a) => a && /^[\p{Script=Latin}\s.'\-()]+$/u.test(a)).slice(0, 8).join(',');

for await (const line of rl) {
  if (!line) continue;
  const c = line.split('\t');
  const [id, name, ascii, alts, lat, lon] = [c[0], c[1], c[2], c[3], c[4], c[5]];
  const cc = c[8], adm1 = c[10];
  const cname = country.get(cc) || cc;
  const rname = region.get(`${cc}.${adm1}`) || '';
  if (!id || !name || !lat || !lon || !cc) continue;
  gz.write(`geonames:${id}\t${clean(name)}\t${clean(ascii)}\t${latinAlts(alts)}\t${cc}\t${clean(cname)}\t${clean(rname)}\t${lat}\t${lon}\n`);
  n++;
}
gz.end();
out.on('close', () => console.log(`Wrote ${n} rows -> cities500.seed.tsv.gz`));
