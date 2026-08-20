import { chromium, request as pwRequest } from '@playwright/test';
import fs from 'node:fs';

const APP = 'http://localhost:3000';
const MAILPIT = 'http://localhost:8025';
const PASSWORD = 'Str0ng!Passw0rd';
const png = Buffer.from(fs.readFileSync('/tmp/claude-0/-home-user-juggerhub/70a6d951-0bef-522e-8779-3da075db5836/scratchpad/png.b64', 'utf8'), 'base64');

const suffix = `${Date.now()}`.slice(-8);
const handle = `shot-${suffix}`;
const email = `shot-${suffix}@example.com`;

const browser = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome' });
const context = await browser.newContext({ baseURL: APP, viewport: { width: 1280, height: 900 } });
const page = await context.newPage();
const api = context.request;

// terms version
const legal = await (await api.get('/i18n/legal/en.json')).json();
const version = legal?.terms?.version ?? '1.0';

let r = await api.post('/api/v1/auth/register', { data: {
  email, password: PASSWORD, handle, displayName: 'Ada Kessler',
  acceptsTerms: true, termsVersion: version, termsLanguage: 'en',
}});
console.log('register', r.status());

// verification link from Mailpit
const mp = await pwRequest.newContext();
const msgs = await (await mp.get(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(email)}`)).json();
const id = msgs.messages?.[0]?.ID;
const body = await (await mp.get(`${MAILPIT}/api/v1/message/${id}`)).json();
const link = (body.HTML || body.Text).match(/https?:\/\/[^\s"'<>]*verify-email[^\s"'<>]*/i)?.[0];
const url = new URL(link);
await page.goto(url.pathname + url.search);
await page.waitForTimeout(1500);

await page.goto('/sign-in');
await page.getByLabel('Email').fill(email);
await page.locator('input[type="password"]').first().fill(PASSWORD);
await page.getByRole('button', { name: /sign in/i }).click();
await page.waitForTimeout(3000);

// public profile so an anonymous view can be shot too
r = await api.put('/api/v1/profiles/me', { data: {
  displayName: 'Ada Kessler', description: 'Runner. Chain-curious.', pompfen: ['Stab'], isPublic: true,
}});
console.log('profile', r.status());

const captions = ['Tempelhofer Feld, first tournament', null, 'Warm-up before the final', null, 'Chain practice'];
for (const caption of captions) {
  const created = await api.post('/api/v1/profiles/me/showcase', {
    multipart: { file: { name: 'p.png', mimeType: 'image/png', buffer: png } },
  });
  const dto = await created.json();
  if (caption) {
    await api.patch(`/api/v1/profiles/me/showcase/${dto.id}`, { data: { caption } });
  }
}
console.log('uploaded 5');

await page.goto(`/u/${handle}`);
await page.waitForTimeout(1800);
await page.screenshot({ path: '/tmp/claude-0/-home-user-juggerhub/70a6d951-0bef-522e-8779-3da075db5836/scratchpad/shot-view-mode.png', fullPage: true });

await page.getByTestId('profile-edit').click();
await page.waitForTimeout(1200);
await page.screenshot({ path: '/tmp/claude-0/-home-user-juggerhub/70a6d951-0bef-522e-8779-3da075db5836/scratchpad/shot-edit-mode.png', fullPage: true });
await page.getByTestId('profile-cancel').click();
await page.waitForTimeout(800);


// mobile
const mobile = await browser.newContext({ baseURL: APP, viewport: { width: 375, height: 812 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true });
const mpage = await mobile.newPage();
await mobile.addCookies(await context.cookies());
await mpage.goto(`/u/${handle}`);
await mpage.waitForTimeout(1500);
await mpage.screenshot({ path: '/tmp/claude-0/-home-user-juggerhub/70a6d951-0bef-522e-8779-3da075db5836/scratchpad/shot-view-mobile.png', fullPage: true });
const overflow = await mpage.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
console.log('mobile horizontal overflow px:', overflow);

await browser.close();
console.log('handle', handle);
