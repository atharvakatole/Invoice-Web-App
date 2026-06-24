import { Component, OnInit, OnDestroy, signal, ElementRef, ViewChildren, QueryList, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ThemeService } from '../../core/services/theme.service';
import { AuthService } from '../../core/services/auth.service';
import { ConsentService } from '../../core/services/consent.service';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss'
})
export class LandingComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChildren('reveal') revealEls!: QueryList<ElementRef>;

  themePickerOpen = signal(false);
  scrolled = signal(false);
  year = new Date().getFullYear();

  private observer?: IntersectionObserver;
  private onScroll = () => this.scrolled.set(window.scrollY > 20);

  features = [
    { icon: '⚡', title: 'Lightning-fast invoicing', desc: 'Create branded, GST-ready invoices in seconds with smart line-item autocomplete and saved clients.' },
    { icon: '📊', title: 'Real-time dashboard', desc: 'Track revenue, pending payments, and top clients with live charts that update as money moves.' },
    { icon: '📁', title: 'Projects that connect everything', desc: 'Link invoices, bills, assistants, and schedules under one project — from kickoff to final payment.' },
    { icon: '🧾', title: 'Expense & returns tracking', desc: 'Track what you buy from brands, handle partial returns, and turn client purchases into invoice line items.' },
    { icon: '🔔', title: 'Never miss a deadline', desc: 'Automatic reminders for return dates, overdue invoices, and unpaid assistants.' },
    { icon: '🎨', title: 'Your brand, your way', desc: 'Custom logos, colors, and PDF templates so every invoice looks unmistakably yours.' }
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
  }

  ngOnDestroy() {
    window.removeEventListener('scroll', this.onScroll);
    this.observer?.disconnect();
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
