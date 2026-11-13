import { Component, HostListener, OnInit, OnDestroy, signal, ElementRef, ViewChildren, QueryList, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';
import { ConsentService } from '../../core/services/consent.service';
import { BrandMarkComponent } from '../../shared/components/brand-mark.component';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink, BrandMarkComponent],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss'
})
export class LandingComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChildren('reveal') revealEls!: QueryList<ElementRef>;
  @ViewChildren('storyChapter') storyChapters!: QueryList<ElementRef<HTMLElement>>;

  themePickerOpen = signal(false);
  scrolled = signal(false);
  activeStory = signal(0);
  year = new Date().getFullYear();

  private observer?: IntersectionObserver;
  private storyObserver?: IntersectionObserver;
  private onScroll = () => this.scrolled.set(window.scrollY > 20);

  features = [
    { icon: '↗', title: 'Fast, accurate invoicing', desc: 'Create branded, GST-ready invoices in seconds with smart line-item autocomplete and saved clients.' },
    { icon: '▥', title: 'A clear business overview', desc: 'Track revenue, pending payments, and top clients with live charts that update as money moves.' },
    { icon: '□', title: 'Connected project tracking', desc: 'Link invoices, bills, assistants, and schedules under one project — from kickoff to final payment.' },
    { icon: '≋', title: 'Expense & returns tracking', desc: 'Track what you buy from brands, handle partial returns, and turn client purchases into invoice line items.' },
    { icon: '◷', title: 'Deadline reminders', desc: 'Automatic reminders for return dates, overdue invoices, and unpaid assistants.' },
    { icon: '▧', title: 'Your brand, your way', desc: 'Custom logos, colors, and PDF templates so every invoice looks unmistakably yours.' }
  ];

  steps = [
    { n: '01', title: 'Add your client', desc: 'Start with a client and spin up a project for their work.' },
    { n: '02', title: 'Track the work', desc: 'Log bills, assign assistants, and schedule shoot days as you go.' },
    { n: '03', title: 'Get paid', desc: 'Generate a polished invoice, send it, and watch payments land.' }
  ];

  plans = [
    { name: 'Free', price: '₹0', period: 'forever', features: ['Up to 5 invoices', 'PDF download', 'Basic dashboard', 'Email support'], cta: 'Start free', highlight: false },
    { name: 'Premium', price: '₹499', period: 'per month', features: ['Unlimited invoices & clients', 'Custom branding & templates', 'Bills, projects & assistants', 'Reports & GST exports', 'Priority support'], cta: 'Go Premium', highlight: true }
  ];

  stories = [
    {
      label: 'SERVICES',
      title: 'Good work deserves a clear paper trail.',
      description: 'Shape an estimate into a polished invoice, then keep the project and payment in view.',
      image: 'assets/images/scene-documents.jpg',
      alt: 'Invoice papers, a calculator, and pen arranged as a document still life on a dark desk.'
    },
    {
      label: 'BRAND IDENTITY',
      title: 'Make every document feel like yours.',
      description: 'Carry your logo, colors, and business details through the invoices and PDFs you share.',
      image: 'assets/images/scene-brand-identity.jpg',
      alt: 'Brand identity stationery, color swatches, and paper studies arranged on a design table.'
    },
    {
      label: 'PROJECT WORKSPACE',
      title: 'Keep the moving parts together.',
      description: 'Bring client work, bills, assignments, and scheduled days into one project view.',
      image: 'assets/images/scene-project-workspace.jpg',
      alt: 'A camera beside an open blank planner, blueprint, and material samples on a work surface.'
    },
    {
      label: 'EVENT EXPERIENCES',
      title: 'Plan the day. Stay with the details.',
      description: 'Keep dates, team assignments, and billing connected while the event comes together.',
      image: 'assets/images/scene-event-space.jpg',
      alt: 'An empty staged venue with sculptural lighting and arranged furnishings ready for an event.'
    },
    {
      label: 'PAYMENTS',
      title: 'Know where every payment stands.',
      description: 'See what has been paid, what is due, and what needs a follow-up at a glance.',
      image: 'assets/images/scene-payments.jpg',
      alt: 'A smartphone with a checkmark beside a receipt, ledger, and pen on a dark counter.'
    }
  ];

  constructor(public theme: ThemeService, private auth: AuthService, private router: Router, private consent: ConsentService) {}

  ngOnInit() {
    if (this.auth.isAuthenticated()) {
      this.router.navigate(['/app/dashboard']);
      return;
    }
    window.addEventListener('scroll', this.onScroll, { passive: true });
  }

  ngAfterViewInit() {
    this.observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            entry.target.classList.add('in-view');
            this.observer?.unobserve(entry.target);
          }
        }
      },
      { threshold: 0.15, rootMargin: '0px 0px -60px 0px' }
    );
    this.revealEls.forEach(el => this.observer?.observe(el.nativeElement));
    // Also observe any elements added after initial render (ngFor items)
    this.revealEls.changes.subscribe((list: QueryList<ElementRef>) => {
      list.forEach(el => this.observer?.observe(el.nativeElement));
    });

    this.storyObserver = new IntersectionObserver((entries) => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((a, b) => Math.abs(a.boundingClientRect.top + a.boundingClientRect.height / 2 - window.innerHeight / 2)
          - Math.abs(b.boundingClientRect.top + b.boundingClientRect.height / 2 - window.innerHeight / 2))[0];
      if (!visible) return;
      const index = Number((visible.target as HTMLElement).dataset['storyIndex']);
      if (Number.isInteger(index)) this.activeStory.set(Math.max(0, Math.min(index, this.stories.length - 1)));
    }, { rootMargin: '-42% 0px -42% 0px', threshold: 0 });
    this.storyChapters.forEach(chapter => this.storyObserver?.observe(chapter.nativeElement));
  }

  ngOnDestroy() {
    window.removeEventListener('scroll', this.onScroll);
    this.observer?.disconnect();
    this.storyObserver?.disconnect();
  }

  @HostListener('document:click', ['$event'])
  onDocClick(e: MouseEvent) {
    const target = e.target as HTMLElement;
    if (!target.closest('.theme-picker')) {
      this.themePickerOpen.set(false);
    }
  }

  selectTheme(id: string) {
    this.theme.apply(id);
    this.themePickerOpen.set(false);
  }

  scrollTo(id: string) {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
  }

  openCookieSettings() {
    this.consent.reopen();
  }
}
