import { safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it('accepts internal single-slash paths, preserving query strings', () => {
    expect(safeReturnUrl('/join/berlin/tok?action=accept')).toBe('/join/berlin/tok?action=accept');
    expect(safeReturnUrl('/')).toBe('/');
  });

  it('rejects absolute and protocol-relative URLs (open-redirect guard)', () => {
    expect(safeReturnUrl('https://evil.example.com')).toBeNull();
    expect(safeReturnUrl('//evil.example.com')).toBeNull();
    expect(safeReturnUrl('javascript:alert(1)')).toBeNull();
  });

  it('rejects empty and missing values', () => {
    expect(safeReturnUrl('')).toBeNull();
    expect(safeReturnUrl(null)).toBeNull();
    expect(safeReturnUrl(undefined)).toBeNull();
  });
});
