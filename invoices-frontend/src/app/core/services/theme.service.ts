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
        '--surface-3': '#182242',
        '--navy': '#0f1b3d',
        '--text': '#eef1fb',
        '--text-dim': '#98a2c3',
        '--text-faint': '#5e6890',
        '--accent': '#4f7cff',
        '--accent-2': '#22d3ee',
        '--gold': '#f5c451',
        '--border': 'rgba(148,163,209,0.12)',
        '--border-strong': 'rgba(148,163,209,0.22)',
        '--gradient-brand': 'linear-gradient(135deg, #4f7cff 0%, #22d3ee 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #121b32 0%, #0a0f1f 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(79,124,255,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(34,211,238,0.10), transparent 55%), #05070e',
        '--accent-tint': 'rgba(79,124,255,0.16)',
        '--accent-tint-strong': 'rgba(79,124,255,0.30)',
        '--brand-shadow': 'rgba(79,124,255,0.35)',
        '--overlay-soft': 'rgba(255,255,255,0.03)',
        '--overlay-mid': 'rgba(255,255,255,0.07)'
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
        '--surface-3': '#3a1a1a',
        '--navy': '#2a1010',
        '--text': '#eef1fb',
        '--text-dim': '#98a2c3',
        '--text-faint': '#5e6890',
        '--accent': '#ff5b5b',
        '--accent-2': '#ff9d4d',
        '--gold': '#ffcf6b',
        '--border': 'rgba(255,180,160,0.12)',
        '--border-strong': 'rgba(255,180,160,0.22)',
        '--gradient-brand': 'linear-gradient(135deg, #ff5b5b 0%, #ff9d4d 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #2a1414 0%, #160a0a 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(255,91,91,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(255,157,77,0.10), transparent 55%), #0c0606',
        '--accent-tint': 'rgba(255,91,91,0.16)',
        '--accent-tint-strong': 'rgba(255,91,91,0.30)',
        '--brand-shadow': 'rgba(255,91,91,0.35)',
        '--overlay-soft': 'rgba(255,255,255,0.03)',
        '--overlay-mid': 'rgba(255,255,255,0.07)'
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
        '--surface-3': '#16382b',
        '--navy': '#0c2a20',
        '--text': '#eef1fb',
        '--text-dim': '#98a2c3',
        '--text-faint': '#5e6890',
        '--accent': '#34d399',
        '--accent-2': '#2dd4bf',
        '--gold': '#a7f3d0',
        '--border': 'rgba(160,230,200,0.12)',
        '--border-strong': 'rgba(160,230,200,0.22)',
        '--gradient-brand': 'linear-gradient(135deg, #34d399 0%, #2dd4bf 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #112b21 0%, #08160f 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(52,211,153,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(45,212,191,0.10), transparent 55%), #040c09',
        '--accent-tint': 'rgba(52,211,153,0.16)',
        '--accent-tint-strong': 'rgba(52,211,153,0.30)',
        '--brand-shadow': 'rgba(52,211,153,0.35)',
        '--overlay-soft': 'rgba(255,255,255,0.03)',
        '--overlay-mid': 'rgba(255,255,255,0.07)'
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
        '--surface-3': '#2c1a4a',
        '--navy': '#1c1038',
        '--text': '#eef1fb',
        '--text-dim': '#98a2c3',
        '--text-faint': '#5e6890',
        '--accent': '#a855f7',
        '--accent-2': '#ec4899',
        '--gold': '#f0abfc',
        '--border': 'rgba(210,180,245,0.12)',
        '--border-strong': 'rgba(210,180,245,0.22)',
        '--gradient-brand': 'linear-gradient(135deg, #a855f7 0%, #ec4899 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #21163b 0%, #110a1e 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(168,85,247,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(236,72,153,0.10), transparent 55%), #0a0612',
        '--accent-tint': 'rgba(168,85,247,0.16)',
        '--accent-tint-strong': 'rgba(168,85,247,0.30)',
        '--brand-shadow': 'rgba(168,85,247,0.35)',
        '--overlay-soft': 'rgba(255,255,255,0.03)',
        '--overlay-mid': 'rgba(255,255,255,0.07)'
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
        '--surface-3': '#3a2e1f',
        '--navy': '#2a2117',
        '--text': '#eef1fb',
        '--text-dim': '#98a2c3',
        '--text-faint': '#5e6890',
        '--accent': '#d4a574',
        '--accent-2': '#e8c39e',
        '--gold': '#f0d9b5',
        '--border': 'rgba(230,210,180,0.12)',
        '--border-strong': 'rgba(230,210,180,0.22)',
        '--gradient-brand': 'linear-gradient(135deg, #d4a574 0%, #e8c39e 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #2b2117 0%, #16110b 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(212,165,116,0.16), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(232,195,158,0.10), transparent 55%), #0e0b07',
        '--accent-tint': 'rgba(212,165,116,0.16)',
        '--accent-tint-strong': 'rgba(212,165,116,0.30)',
        '--brand-shadow': 'rgba(212,165,116,0.35)',
        '--overlay-soft': 'rgba(255,255,255,0.03)',
        '--overlay-mid': 'rgba(255,255,255,0.07)'
      }
    },
    {
      id: 'daylight',
      name: 'Daylight',
      swatch: 'linear-gradient(135deg, #2563eb, #06b6d4)',
      vars: {
        '--bg': '#f4f6fb',
        '--bg-soft': '#ffffff',
        '--surface': '#ffffff',
        '--surface-2': '#f0f3f9',
        '--surface-3': '#e4e9f2',
        '--navy': '#dbe3f0',
        '--text': '#1a2236',
        '--text-dim': '#55617d',
        '--text-faint': '#8a93a8',
        '--accent': '#2563eb',
        '--accent-2': '#0891b2',
        '--gold': '#d99e2b',
        '--border': 'rgba(30,50,90,0.10)',
        '--border-strong': 'rgba(30,50,90,0.18)',
        '--gradient-brand': 'linear-gradient(135deg, #2563eb 0%, #06b6d4 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #ffffff 0%, #f4f6fb 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(37,99,235,0.10), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(8,145,178,0.08), transparent 55%), #f4f6fb',
        '--accent-tint': 'rgba(37,99,235,0.16)',
        '--accent-tint-strong': 'rgba(37,99,235,0.30)',
        '--brand-shadow': 'rgba(37,99,235,0.35)',
        '--overlay-soft': 'rgba(20,30,55,0.04)',
        '--overlay-mid': 'rgba(20,30,55,0.08)'
      }
    },
    {
      id: 'blossom',
      name: 'Blossom',
      swatch: 'linear-gradient(135deg, #e11d76, #f59e0b)',
      vars: {
        '--bg': '#fdf5f7',
        '--bg-soft': '#ffffff',
        '--surface': '#ffffff',
        '--surface-2': '#fceef3',
        '--surface-3': '#f7e0e9',
        '--navy': '#f3d4e0',
        '--text': '#1a2236',
        '--text-dim': '#55617d',
        '--text-faint': '#8a93a8',
        '--accent': '#e11d76',
        '--accent-2': '#f43f5e',
        '--gold': '#e0991b',
        '--border': 'rgba(120,30,70,0.10)',
        '--border-strong': 'rgba(120,30,70,0.18)',
        '--gradient-brand': 'linear-gradient(135deg, #e11d76 0%, #fb7185 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #ffffff 0%, #fdf5f7 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(225,29,118,0.08), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(244,63,94,0.07), transparent 55%), #fdf5f7',
        '--accent-tint': 'rgba(225,29,118,0.16)',
        '--accent-tint-strong': 'rgba(225,29,118,0.30)',
        '--brand-shadow': 'rgba(225,29,118,0.35)',
        '--overlay-soft': 'rgba(20,30,55,0.04)',
        '--overlay-mid': 'rgba(20,30,55,0.08)'
      }
    },
    {
      id: 'mint',
      name: 'Mint',
      swatch: 'linear-gradient(135deg, #059669, #0d9488)',
      vars: {
        '--bg': '#f1f9f5',
        '--bg-soft': '#ffffff',
        '--surface': '#ffffff',
        '--surface-2': '#e8f5ee',
        '--surface-3': '#d8ede1',
        '--navy': '#c8e6d6',
        '--text': '#1a2236',
        '--text-dim': '#55617d',
        '--text-faint': '#8a93a8',
        '--accent': '#059669',
        '--accent-2': '#0d9488',
        '--gold': '#4d9e7a',
        '--border': 'rgba(10,80,55,0.10)',
        '--border-strong': 'rgba(10,80,55,0.18)',
        '--gradient-brand': 'linear-gradient(135deg, #059669 0%, #0d9488 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #ffffff 0%, #f1f9f5 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(5,150,105,0.09), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(13,148,136,0.07), transparent 55%), #f1f9f5',
        '--accent-tint': 'rgba(5,150,105,0.16)',
        '--accent-tint-strong': 'rgba(5,150,105,0.30)',
        '--brand-shadow': 'rgba(5,150,105,0.35)',
        '--overlay-soft': 'rgba(20,30,55,0.04)',
        '--overlay-mid': 'rgba(20,30,55,0.08)'
      }
    },
    {
      id: 'linen',
      name: 'Linen',
      swatch: 'linear-gradient(135deg, #b45309, #d97706)',
      vars: {
        '--bg': '#faf6f0',
        '--bg-soft': '#ffffff',
        '--surface': '#ffffff',
        '--surface-2': '#f4ece0',
        '--surface-3': '#ece0cf',
        '--navy': '#e2d3bd',
        '--text': '#1a2236',
        '--text-dim': '#55617d',
        '--text-faint': '#8a93a8',
        '--accent': '#b45309',
        '--accent-2': '#d97706',
        '--gold': '#a07333',
        '--border': 'rgba(90,60,20,0.10)',
        '--border-strong': 'rgba(90,60,20,0.18)',
        '--gradient-brand': 'linear-gradient(135deg, #b45309 0%, #d97706 100%)',
        '--gradient-surface': 'linear-gradient(160deg, #ffffff 0%, #faf6f0 100%)',
        '--gradient-bg': 'radial-gradient(ellipse 1200px 800px at 80% -10%, rgba(180,83,9,0.08), transparent 60%), radial-gradient(ellipse 900px 700px at -10% 110%, rgba(217,119,6,0.07), transparent 55%), #faf6f0',
        '--accent-tint': 'rgba(180,83,9,0.16)',
        '--accent-tint-strong': 'rgba(180,83,9,0.30)',
        '--brand-shadow': 'rgba(180,83,9,0.35)',
        '--overlay-soft': 'rgba(20,30,55,0.04)',
        '--overlay-mid': 'rgba(20,30,55,0.08)'
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
