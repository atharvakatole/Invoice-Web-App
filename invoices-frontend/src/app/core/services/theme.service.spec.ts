import { describe, expect, it, beforeEach } from 'vitest';
import { ThemeService } from './theme.service';

describe('ThemeService editorial theme regression', () => {
  let service: ThemeService;

  beforeEach(() => {
    document.cookie = 'invoicely_theme=; Max-Age=0; path=/';
    document.cookie = 'invoicely_consent=; Max-Age=0; path=/';
    document.documentElement.removeAttribute('style');
    service = new ThemeService();
  });

  it('uses Linen for a first visit while retaining each available theme', () => {
    service.init();
    expect(service.activeId()).toBe('linen');
    expect(service.themes).toHaveLength(9);
  });

  it('restores a valid saved theme on initialization', () => {
    document.cookie = 'invoicely_theme=forest; path=/';
    service.init();
    expect(service.activeId()).toBe('forest');
    expect(document.documentElement.style.getPropertyValue('--accent')).toBe('#34d399');
  });

  it('updates the theme reactive surfaces and readable button text for every theme', () => {
    for (const theme of service.themes) {
      service.apply(theme.id, false);
      expect(service.activeId()).toBe(theme.id);
      expect(document.documentElement.style.getPropertyValue('--accent')).toBe(theme.vars['--accent']);
      expect(document.documentElement.style.getPropertyValue('--on-accent')).toBe(theme.vars['--on-accent']);
      expect(document.documentElement.style.getPropertyValue('--gradient-brand')).toBe('var(--accent)');
      expect(document.documentElement.style.getPropertyValue('--gradient-surface')).toBe('var(--surface)');
    }
  });

  it('ignores unknown theme ids without disturbing the active theme', () => {
    service.apply('linen', false);
    service.apply('missing-theme', false);
    expect(service.activeId()).toBe('linen');
  });
});
