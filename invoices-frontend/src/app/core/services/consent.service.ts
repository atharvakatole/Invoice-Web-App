import { Injectable, signal } from '@angular/core';

export type ConsentChoice = 'all' | 'functional' | 'rejected' | null;

@Injectable({ providedIn: 'root' })
export class ConsentService {
  private readonly COOKIE = 'invoicely_consent';
  choice = signal<ConsentChoice>(null);
  showBanner = signal(false);

  init() {
    const saved = this.readCookie(this.COOKIE) as ConsentChoice;
    if (saved) {
      this.choice.set(saved);
      this.showBanner.set(false);
    } else {
      this.showBanner.set(true);
    }
  }

  accept(choice: Exclude<ConsentChoice, null>) {
    this.choice.set(choice);
    this.writeCookie(this.COOKIE, choice, 365);
    this.showBanner.set(false);
  }

  reopen() {
    this.showBanner.set(true);
  }

  private readCookie(name: string): string | null {
    const m = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
    return m ? decodeURIComponent(m[1]) : null;
  }

  private writeCookie(name: string, value: string, days: number) {
    const expires = new Date(Date.now() + days * 864e5).toUTCString();
    document.cookie = `${name}=${encodeURIComponent(value)}; expires=${expires}; path=/; SameSite=Lax`;
  }
}
