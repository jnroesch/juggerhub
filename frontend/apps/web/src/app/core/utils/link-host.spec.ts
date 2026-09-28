import { linkHost } from './link-host';

describe('linkHost (feature 061)', () => {
  it('shows the site, without www.', () => {
    expect(linkHost('https://www.instagram.com/rheinfeuer')).toBe('instagram.com');
    expect(linkHost('https://discord.gg/abc')).toBe('discord.gg');
  });

  it('shows a lookalike internationalised host in its encoded form', () => {
    // A Cyrillic "і" (U+0456) in place of the Latin "i".
    const host = linkHost('https://іnstagram.com/rheinfeuer');
    expect(host.startsWith('xn--')).toBe(true);
    expect(host).not.toBe('instagram.com');
  });

  it('keeps a port, which is part of where the link goes', () => {
    expect(linkHost('https://rheinfeuer.de:8443/x')).toBe('rheinfeuer.de:8443');
  });

  it('keeps a www inside the name', () => {
    expect(linkHost('https://blog.www.example')).toBe('blog.www.example');
  });

  it('yields nothing for something that is not an address', () => {
    expect(linkHost('not a url')).toBe('');
  });
});
