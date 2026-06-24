import { Component, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastComponent } from './shared/components/toast.component';
import { CookieConsentComponent } from './shared/components/cookie-consent/cookie-consent.component';
import { ThemeService } from './core/services/theme.service';
import { ConsentService } from './core/services/consent.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastComponent, CookieConsentComponent],
  template: `<router-outlet></router-outlet><app-toast></app-toast><app-cookie-consent></app-cookie-consent>`
})
export class AppComponent implements OnInit {
  constructor(private theme: ThemeService, private consent: ConsentService) {}

  ngOnInit() {
    this.theme.init();
    this.consent.init();
  }
}
