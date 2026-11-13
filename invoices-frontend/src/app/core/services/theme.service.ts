import { Injectable, signal } from '@angular/core';

export interface ThemeOption {
  id: 'dark' | 'light';
  name: 'Dark' | 'Light';
  swatch: string;
  vars: Record<string, string>;
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly themes: ThemeOption[] = [
    {
      id: 'dark',
      name: 'Dark',
      swatch: '#050608',
      vars: {
        '--bg': '#050608',
        '--bg-soft': '#0a0d12',
        '--surface': '#10141b',
        '--surface-2': '#171d27',
        '--surface-3': '#222c3a',
        '--navy': '#101a30',
        '--text': '#f4f6fb',
        '--text-dim': '#c1c8d5',
        '--text-faint': '#8d99ab',
        '--accent': '#315de5',
        '--accent-2': '#78aaff',
        '--gold': '#e7bf72',
        '--on-accent': '#ffffff',
        '--border': 'rgba(178, 195, 225, 0.14)',
        '--border-strong': 'rgba(178, 195, 225, 0.28)',
        '--gradient-brand': 'var(--accent)',
        '--gradient-surface': 'var(--surface)',
        '--gradient-bg': 'var(--bg)',
        '--gradient-gold': 'var(--gold)',
        '--accent-tint': 'rgba(49, 93, 229, 0.18)',
        '--accent-tint-strong': 'rgba(49, 93, 229, 0.34)',
        '--brand-shadow': 'rgba(49, 93, 229, 0.28)',
        '--overlay-soft': 'rgba(255, 255, 255, 0.04)',
        '--overlay-mid': 'rgba(255, 255, 255, 0.08)'
      }
    },
    {
      id: 'light',
      name: 'Light',
      swatch: '#f2f4f8',
      vars: {
        '--bg': '#f2f4f8',
        '--bg-soft': '#ffffff',
        '--surface': '#ffffff',
        '--surface-2': '#e9edf4',
        '--surface-3': '#dce3ed',
        '--navy': '#d8e0ec',
        '--text': '#172033',
        '--text-dim': '#4d5b72',
        '--text-faint': '#65748a',
        '--accent': '#174dcc',
        '--accent-2': '#126db4',
        '--gold': '#8a5d0b',
        '--on-accent': '#ffffff',
        '--border': 'rgba(34, 51, 79, 0.14)',
        '--border-strong': 'rgba(34, 51, 79, 0.25)',
        '--gradient-brand': 'var(--accent)',
        '--gradient-surface': 'var(--surface)',
        '--gradient-bg': 'var(--bg)',
        '--gradient-gold': 'var(--gold)',
        '--accent-tint': 'rgba(23, 77, 204, 0.12)',
        '--accent-tint-strong': 'rgba(23, 77, 204, 0.24)',
        '--brand-shadow': 'rgba(23, 77, 204, 0.18)',
        '--overlay-soft': 'rgba(23, 32, 51, 0.04)',
        '--overlay-mid': 'rgba(23, 32, 51, 0.08)'
      }
    }
  ];

  activeId = signal<'dark' | 'light'>('dark');

  private readonly COOKIE = 'invoicely_theme';

  init() {
    const saved = this.readCookie(this.COOKIE);
    const theme = this.themes.find(item => item.id === saved) ?? this.themes[0];
    this.apply(theme.id, false);
  }

  apply(id: string, persist = true) {
    const theme = this.themes.find(item => item.id === id);
    if (!theme) return;

    this.activeId.set(theme.id);
    const root = document.documentElement;
    for (const [key, value] of Object.entries(theme.vars)) root.style.setProperty(key, value);

    if (persist && this.functionalCookiesAllowed()) this.writeCookie(this.COOKIE, theme.id, 365);
  }

  private functionalCookiesAllowed(): boolean {
    return this.readCookie('invoicely_consent') !== 'rejected';
  }

  private readCookie(name: string): string | null {
    const match = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
    return match ? decodeURIComponent(match[1]) : null;
  }

  private writeCookie(name: string, value: string, days: number) {
    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie = `${name}=${encodeURIComponent(value)}; expires=${expires}; path=/; SameSite=Lax`;
  }
}
