import { beforeEach, describe, expect, it } from 'vitest';
import { ThemeService } from './theme.service';

describe('ThemeService two-theme contract', () => {
  let service: ThemeService;

  beforeEach(() => {
    document.cookie = 'invoicely_theme=; Max-Age=0; path=/';
    document.cookie = 'invoicely_consent=; Max-Age=0; path=/';
    document.documentElement.removeAttribute('style');
    service = new ThemeService();
  });

  it('offers exactly Dark and Light, defaulting new visitors to the black and blue theme', () => {
    service.init();
    expect(service.themes.map(theme => theme.name)).toEqual(['Dark', 'Light']);
    expect(service.themes.map(theme => theme.id)).toEqual(['dark', 'light']);
    expect(service.activeId()).toBe('dark');
    expect(document.documentElement.style.getPropertyValue('--bg')).toBe('#050608');
    expect(document.documentElement.style.getPropertyValue('--accent')).toBe('#315de5');
  });

  it('restores either supported saved theme and falls back for retired theme cookies', () => {
    document.cookie = 'invoicely_theme=light; path=/';
    service.init();
    expect(service.activeId()).toBe('light');
    expect(document.documentElement.style.getPropertyValue('--bg')).toBe('#f2f4f8');

    document.cookie = 'invoicely_theme=forest; path=/';
    service.init();
    expect(service.activeId()).toBe('dark');
  });

  it('updates surfaces, accent, and accessible button text for each supported theme', () => {
    for (const theme of service.themes) {
      service.apply(theme.id, false);
      expect(service.activeId()).toBe(theme.id);
      expect(document.documentElement.style.getPropertyValue('--bg')).toBe(theme.vars['--bg']);
      expect(document.documentElement.style.getPropertyValue('--accent')).toBe(theme.vars['--accent']);
      expect(document.documentElement.style.getPropertyValue('--on-accent')).toBe('#ffffff');
    }
  });

  it('ignores unsupported theme ids without changing the active theme', () => {
    service.apply('light', false);
    service.apply('forest', false);
    expect(service.activeId()).toBe('light');
  });
});
