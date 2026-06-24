import { Injectable, signal } from '@angular/core';

export interface ThemeOption {
  id: string;
  name: string;
  /** Swatch shown in the picker */
  swatch: string;
  vars: Record<string, string>;
}

/**
 * Dynamic theme system for the marketing landing page (and optionally the app).
 * Each theme overrides a small set of CSS custom properties on :root.
 * The choice is persisted in a cookie so it survives reloads and works with
 * the consent banner ("functional" cookie).
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly themes: ThemeOption[] = [
    {
      id: 'midnight',
      name: 'Midnight',
      swatch: 'linear-gradient(135deg, #4f7cff, #22d3ee)',
      vars: {
        '--bg': '#05070e',
        '--bg-soft': '#080c18',
        '--surface': '#0d1424',
        '--surface-2': '#121b32',
        '--accent': '#4f7cff',
        '--accent-2': '#22d3ee',
        '--gold': '#f5c451',
        '--gradient-brand': 'linear-gradient(135deg, #4f7cff 0%, #22d3ee 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(79,124,255,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(34,211,238,0.10), transparent 55%), #05070e',
        '--surface-3': '#182242',
        '--navy': '#0f1b3d',
        '--border': 'rgba(148, 163, 209, 0.12)',
        '--border-strong': 'rgba(148, 163, 209, 0.22)',
        '--gradient-surface': 'linear-gradient(160deg, #121b32 0%, #0a0f1f 100%)'
      }
    },
    {
      id: 'ember',
      name: 'Ember',
      swatch: 'linear-gradient(135deg, #ff5b5b, #ff9d4d)',
      vars: {
        '--bg': '#0c0606',
        '--bg-soft': '#140a0a',
        '--surface': '#1c0f0f',
        '--surface-2': '#2a1414',
        '--accent': '#ff5b5b',
        '--accent-2': '#ff9d4d',
        '--gold': '#ffcf6b',
        '--gradient-brand': 'linear-gradient(135deg, #ff5b5b 0%, #ff9d4d 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(255,91,91,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(255,157,77,0.10), transparent 55%), #0c0606',
        '--surface-3': '#3a1a1a',
        '--navy': '#2a1010',
        '--border': 'rgba(255, 180, 160, 0.12)',
        '--border-strong': 'rgba(255, 180, 160, 0.22)',
        '--gradient-surface': 'linear-gradient(160deg, #2a1414 0%, #160a0a 100%)'
      }
    },
    {
      id: 'forest',
      name: 'Forest',
      swatch: 'linear-gradient(135deg, #34d399, #2dd4bf)',
      vars: {
        '--bg': '#040c09',
        '--bg-soft': '#07140f',
        '--surface': '#0c1f18',
        '--surface-2': '#112b21',
        '--accent': '#34d399',
        '--accent-2': '#2dd4bf',
        '--gold': '#a7f3d0',
        '--gradient-brand': 'linear-gradient(135deg, #34d399 0%, #2dd4bf 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(52,211,153,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(45,212,191,0.10), transparent 55%), #040c09',
        '--surface-3': '#16382b',
        '--navy': '#0c2a20',
        '--border': 'rgba(160, 230, 200, 0.12)',
        '--border-strong': 'rgba(160, 230, 200, 0.22)',
        '--gradient-surface': 'linear-gradient(160deg, #112b21 0%, #08160f 100%)'
      }
    },
    {
      id: 'royal',
      name: 'Royal',
      swatch: 'linear-gradient(135deg, #a855f7, #ec4899)',
      vars: {
        '--bg': '#0a0612',
        '--bg-soft': '#100a1e',
        '--surface': '#180f2c',
        '--surface-2': '#21163b',
        '--accent': '#a855f7',
        '--accent-2': '#ec4899',
        '--gold': '#f0abfc',
        '--gradient-brand': 'linear-gradient(135deg, #a855f7 0%, #ec4899 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(168,85,247,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(236,72,153,0.10), transparent 55%), #0a0612',
        '--surface-3': '#2c1a4a',
        '--navy': '#1c1038',
        '--border': 'rgba(210, 180, 245, 0.12)',
        '--border-strong': 'rgba(210, 180, 245, 0.22)',
        '--gradient-surface': 'linear-gradient(160deg, #21163b 0%, #110a1e 100%)'
      }
    },
    {
      id: 'sand',
      name: 'Sand',
      swatch: 'linear-gradient(135deg, #d4a574, #e8c39e)',
      vars: {
        '--bg': '#0e0b07',
        '--bg-soft': '#16110b',
        '--surface': '#1f1810',
        '--surface-2': '#2b2117',
        '--accent': '#d4a574',
        '--accent-2': '#e8c39e',
        '--gold': '#f0d9b5',
        '--gradient-brand': 'linear-gradient(135deg, #d4a574 0%, #e8c39e 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(212,165,116,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(232,195,158,0.10), transparent 55%), #0e0b07',
        '--surface-3': '#3a2e1f',
        '--navy': '#2a2117',
        '--border': 'rgba(230, 210, 180, 0.12)',
        '--border-strong': 'rgba(230, 210, 180, 0.22)',
        '--gradient-surface': 'linear-gradient(160deg, #2b2117 0%, #16110b 100%)'
      }
    }
  ];

  activeId = signal<string>('midnight');

  private readonly COOKIE = 'invoicely_theme';

  init() {
    const saved = this.readCookie(this.COOKIE);
    const theme = this.themes.find(t => t.id === saved) ?? this.themes[0];
    this.apply(theme.id, false);
  }

  apply(id: string, persist = true) {
    const theme = this.themes.find(t => t.id === id);
    if (!theme) return;
    this.activeId.set(id);

    const root = document.documentElement;
    for (const [key, value] of Object.entries(theme.vars)) {
      root.style.setProperty(key, value);
    }

    // Only persist if the user has accepted functional cookies (or no banner shown).
    if (persist && this.functionalCookiesAllowed()) {
      this.writeCookie(this.COOKIE, id, 365);
    }
  }

  private functionalCookiesAllowed(): boolean {
    const consent = this.readCookie('invoicely_consent');
    // 'all' or 'functional' allows; if no decision yet, allow (theme is non-tracking).
    return consent !== 'rejected';
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
