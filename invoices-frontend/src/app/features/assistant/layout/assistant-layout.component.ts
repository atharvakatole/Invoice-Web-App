import { Component, HostListener, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-assistant-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './assistant-layout.component.html',
  styleUrl: './assistant-layout.component.scss'
})
export class AssistantLayoutComponent {
  profileMenuOpen = signal(false);
  themeMenuOpen = signal(false);

  constructor(public auth: AuthService, public theme: ThemeService, private router: Router) {}

  initials(): string {
    const name = this.auth.user()?.fullName ?? '';
    return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
  }

  logout() { this.auth.logout(); }

  switchToManager() {
    this.auth.setWorkMode('manager');
    this.router.navigate(['/app/dashboard']);
  }

  @HostListener('document:click', ['$event'])
  onDocClick(e: MouseEvent) {
    const t = e.target as HTMLElement;
    if (!t.closest('.profile-menu-wrap')) this.profileMenuOpen.set(false);
    if (!t.closest('.theme-picker-app')) this.themeMenuOpen.set(false);
  }

  pickTheme(id: string) { this.theme.apply(id); this.themeMenuOpen.set(false); }
}
