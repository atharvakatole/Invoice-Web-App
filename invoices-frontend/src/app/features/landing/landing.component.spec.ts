import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LandingComponent } from './landing.component';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';
import { ConsentService } from '../../core/services/consent.service';

describe('LandingComponent editorial workflow preview', () => {
  const theme = { apply: vi.fn(), themes: [], activeId: () => 'midnight' };
  const auth = { isAuthenticated: () => false };
  const consent = { reopen: vi.fn() };

  beforeEach(async () => {
    vi.stubGlobal('IntersectionObserver', class {
      observe() {}
      unobserve() {}
      disconnect() {}
    });
    theme.apply.mockClear();
    consent.reopen.mockClear();
    await TestBed.configureTestingModule({
      imports: [LandingComponent],
      providers: [
        provideRouter([]),
        { provide: ThemeService, useValue: theme },
        { provide: AuthService, useValue: auth },
        { provide: ConsentService, useValue: consent }
      ]
    }).compileComponents();
  });

  it('renders four explicitly illustrative workflow tiles without claiming live account data', () => {
    const fixture = TestBed.createComponent(LandingComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;

    expect(element.querySelectorAll('.scene-tile')).toHaveLength(4);
    expect(element.querySelector('.editorial-tiles')?.getAttribute('aria-label')).toContain('Illustrative Invoicely workflow');
    expect(element.querySelectorAll('.scene-stamp')).toHaveLength(3);
    expect(element.querySelector('[aria-label="Sample invoice preview"]')).not.toBeNull();
    expect(element.querySelector('[aria-label="Sample project overview"]')).not.toBeNull();
    expect(element.querySelector('[aria-label="Sample client workflow"]')).not.toBeNull();
    expect(element.querySelector('[aria-label="Sample payment status"]')).not.toBeNull();
    expect((element.textContent?.match(/SAMPLE/g) ?? [])).toHaveLength(4);
    expect(element.textContent).not.toContain('LIVE BILLING PREVIEW');
    fixture.destroy();
  });

  it('preserves the feature descriptions and public sign-in and registration routes', () => {
    const fixture = TestBed.createComponent(LandingComponent);
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    for (const feature of [
      'Fast, accurate invoicing', 'A clear business overview', 'Connected project tracking',
      'Expense & returns tracking', 'Deadline reminders', 'Your brand, your way'
    ]) {
      expect(element.textContent).toContain(feature);
    }
    expect(element.querySelector('a[href="/login"]')).not.toBeNull();
    expect(element.querySelectorAll('a[href="/register"]').length).toBeGreaterThan(0);
    expect(element.textContent).toContain('Start free — no card needed');
    fixture.destroy();
  });

  it('keeps theme selection and cookie settings connected to their existing services', () => {
    const fixture = TestBed.createComponent(LandingComponent);
    const component = fixture.componentInstance;

    component.selectTheme('forest');
    component.openCookieSettings();

    expect(theme.apply).toHaveBeenCalledWith('forest');
    expect(component.themePickerOpen()).toBe(false);
    expect(consent.reopen).toHaveBeenCalledOnce();
    fixture.destroy();
  });
});
