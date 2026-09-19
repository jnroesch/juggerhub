import { convertToParamMap } from '@angular/router';
import { inviteFromQuery, inviteFromReturnUrl, invitePagePath, inviteRef, inviteReturnUrl } from './invite-ref';

/** 43 base64url chars — the shape TeamInvitationService.NewToken() produces. */
const TOKEN = 'Xy9_abcDEF-ghiJKL012mnoPQR345stuVWX678yzAB_';

describe('invite-ref', () => {
  describe('inviteRef', () => {
    it('accepts a well-formed slug and token', () => {
      expect(inviteRef('berlin-jugger', TOKEN)).toEqual({ slug: 'berlin-jugger', token: TOKEN });
    });

    it('rejects a slug that breaks the team-slug rules', () => {
      expect(inviteRef('Bad_Slug', TOKEN)).toBeNull();
      expect(inviteRef('-lead', TOKEN)).toBeNull();
      expect(inviteRef('ab', TOKEN)).toBeNull();
      expect(inviteRef('a'.repeat(31), TOKEN)).toBeNull();
      expect(inviteRef('double--hyphen', TOKEN)).toBeNull();
    });

    it('rejects a token outside the base64url charset or length bounds', () => {
      expect(inviteRef('berlin-jugger', 'spaces here')).toBeNull();
      expect(inviteRef('berlin-jugger', '<script>alert(1)</script>')).toBeNull();
      expect(inviteRef('berlin-jugger', 'tooshort')).toBeNull();
      expect(inviteRef('berlin-jugger', 'a'.repeat(200))).toBeNull();
    });

    it('is both parts or neither', () => {
      expect(inviteRef('berlin-jugger', null)).toBeNull();
      expect(inviteRef(null, TOKEN)).toBeNull();
      expect(inviteRef('', TOKEN)).toBeNull();
      expect(inviteRef(undefined, undefined)).toBeNull();
    });
  });

  describe('inviteFromReturnUrl', () => {
    it('parses the invite page with and without the resumed action', () => {
      expect(inviteFromReturnUrl(`/join/berlin-jugger/${TOKEN}`)).toEqual({ slug: 'berlin-jugger', token: TOKEN });
      expect(inviteFromReturnUrl(`/join/berlin-jugger/${TOKEN}?action=accept`)).toEqual({
        slug: 'berlin-jugger',
        token: TOKEN,
      });
      expect(inviteFromReturnUrl(`/join/berlin-jugger/${TOKEN}?action=decline`)).toEqual({
        slug: 'berlin-jugger',
        token: TOKEN,
      });
    });

    it('is null for anything that is not exactly the invite page', () => {
      expect(inviteFromReturnUrl('/players/nik')).toBeNull();
      expect(inviteFromReturnUrl('/')).toBeNull();
      expect(inviteFromReturnUrl(`//join/berlin-jugger/${TOKEN}`)).toBeNull();
      expect(inviteFromReturnUrl(`https://evil.example.com/join/berlin-jugger/${TOKEN}`)).toBeNull();
      expect(inviteFromReturnUrl(`/join/berlin-jugger/${TOKEN}/extra`)).toBeNull();
      expect(inviteFromReturnUrl(`/join/berlin-jugger/${TOKEN}?action=steal`)).toBeNull();
      expect(inviteFromReturnUrl(`/join/Bad_Slug/${TOKEN}`)).toBeNull();
      expect(inviteFromReturnUrl('/join/berlin-jugger/not a token')).toBeNull();
      expect(inviteFromReturnUrl(null)).toBeNull();
      expect(inviteFromReturnUrl(undefined)).toBeNull();
    });
  });

  describe('inviteFromQuery', () => {
    it('reads both query params', () => {
      expect(inviteFromQuery(convertToParamMap({ inviteSlug: 'berlin-jugger', inviteToken: TOKEN }))).toEqual({
        slug: 'berlin-jugger',
        token: TOKEN,
      });
    });

    it('is null when either is missing or malformed', () => {
      expect(inviteFromQuery(convertToParamMap({ inviteSlug: 'berlin-jugger' }))).toBeNull();
      expect(inviteFromQuery(convertToParamMap({ inviteToken: TOKEN }))).toBeNull();
      expect(inviteFromQuery(convertToParamMap({ inviteSlug: 'x', inviteToken: TOKEN }))).toBeNull();
      expect(inviteFromQuery(convertToParamMap({}))).toBeNull();
    });
  });

  describe('composers', () => {
    const ref = { slug: 'berlin-jugger', token: TOKEN };

    it('inviteReturnUrl resumes the accept after sign-in', () => {
      expect(inviteReturnUrl(ref)).toBe(`/join/berlin-jugger/${TOKEN}?action=accept`);
    });

    it('invitePagePath is the page with no automatic action', () => {
      expect(invitePagePath(ref)).toBe(`/join/berlin-jugger/${TOKEN}`);
    });

    it('round-trips through inviteFromReturnUrl', () => {
      expect(inviteFromReturnUrl(inviteReturnUrl(ref))).toEqual(ref);
      expect(inviteFromReturnUrl(invitePagePath(ref))).toEqual(ref);
    });
  });
});
