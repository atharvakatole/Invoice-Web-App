import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { LandingComponent } from './landing.component';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';
import { ConsentService } from '../../core/services/consent.service';

class TestIntersectionObserver {
  static instances: TestIntersectionObserver[] = [];
  readonly observed: Element[] = [];
  disconnected = false;

  constructor(private readonly callback: IntersectionObserverCallback) {
    TestIntersectionObserver.instances.push(this);
  }

  observe(target: Element) { this.observed.push(target); }
  unobserve(_target: Element) {}
  disconnect() { this.disconnected = true; }
  takeRecords(): IntersectionObserverEntry[] { return []; }

  intersect(target: Element) {
    const entry = {
      target,
      isIntersecting: true,
      intersectionRatio: 1,
      boundingClientRect: target.getBoundingClientRect(),
      intersectionRect: target.getBoundingClientRect(),
      rootBounds: null,
      time: 0
    } as IntersectionObserverEntry;
    this.callback([entry], this as unknown as IntersectionObserver);
  }
}

describe('LandingComponent editorial story', () => {
  const theme = {
    apply: vi.fn(),
    themes: [
      { id: 'dark', name: 'Dark', swatch: '#050608' },
      { id: 'light', name: 'Light', swatch: '#f2f4f8' }
    ],
    activeId: () => 'dark'
  };
  const auth = { isAuthenticated: () => false };
  const consent = { reopen: vi.fn() };

  beforeEach(async () => {
    TestIntersectionObserver.instances = [];
    vi.stubGlobal('IntersectionObserver', TestIntersectionObserver);
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

  afterEach(() => {
    vi.unstubAllGlobals();
    TestBed.resetTestingModule();
  });

  it('renders five distinct local image scenes with meaningful alt text and no live account data', () => {
    const fixture = TestBed.createComponent(LandingComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const element: HTMLElement = fixture.nativeElement;

    expect(component.stories).toHaveLength(5);
    const sources = component.stories.map(story => story.image);
    expect(new Set(sources).size).toBe(5);
    expect(sources.every(source => source.startsWith('assets/images/scene-') && source.endsWith('.jpg'))).toBe(true);
    expect(component.stories.map(story => story.label)).toEqual([
      'SERVICES', 'BRAND IDENTITY', 'PROJECT WORKSPACE', 'EVENT EXPERIENCES', 'PAYMENTS'
    ]);
    expect(component.stories.every(story => story.alt.trim().length > 30)).toBe(true);
    expect(element.querySelectorAll('.story-chapter')).toHaveLength(5);
    expect(element.querySelector('.story-image-caption[aria-live="polite"]')).not.toBeNull();
    expect(element.querySelector('.story-illustration-note')?.textContent).toContain('NO ACCOUNT DATA');
    expect(element.querySelector('.editorial-tiles')).toBeNull();
    expect(element.textContent).not.toContain('₹48,500');
    expect(element.textContent).not.toContain('Payment received');
    fixture.destroy();
  });

  it('changes the sticky image and caption when a story chapter intersects', () => {
    const fixture = TestBed.createComponent(LandingComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const element: HTMLElement = fixture.nativeElement;
    const chapters = Array.from(element.querySelectorAll('.story-chapter'));
    const storyObserver = TestIntersectionObserver.instances.find(observer =>
      observer.observed.some(target => target.classList.contains('story-chapter'))
    );

    expect(storyObserver).toBeDefined();
    expect(storyObserver?.observed).toHaveLength(5);
    storyObserver?.intersect(chapters[2]);
    fixture.detectChanges();

    expect(component.activeStory()).toBe(2);
    expect(element.querySelector('.story-stage img')?.getAttribute('src')).toBe('assets/images/scene-project-workspace.jpg');
    expect(element.querySelector('.story-image-caption')?.textContent).toContain('PROJECT WORKSPACE');
    fixture.destroy();
  });

  it('disconnects observers when the landing page is destroyed', () => {
    const fixture = TestBed.createComponent(LandingComponent);
    fixture.detectChanges();
    expect(TestIntersectionObserver.instances.length).toBeGreaterThan(0);

    fixture.destroy();

    expect(TestIntersectionObserver.instances.every(observer => observer.disconnected)).toBe(true);
  });

  it('preserves feature descriptions and public sign-in and registration routes', () => {
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

  it('offers only Dark and Light themes and keeps cookie settings connected', () => {
    const fixture = TestBed.createComponent(LandingComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const element: HTMLElement = fixture.nativeElement;

    expect(theme.themes.map(option => option.name)).toEqual(['Dark', 'Light']);
    component.themePickerOpen.set(true);
    fixture.detectChanges();
    expect(Array.from(element.querySelectorAll('.theme-option')).map(option => option.textContent?.trim().replace(/\s+/g, ' ')))
      .toEqual(['Dark ✓', 'Light']);

    component.selectTheme('light');
    component.openCookieSettings();

    expect(theme.apply).toHaveBeenCalledWith('light');
    expect(component.themePickerOpen()).toBe(false);
    expect(consent.reopen).toHaveBeenCalledOnce();
    fixture.destroy();
  });
});
