import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConsentService } from '../../../core/services/consent.service';

@Component({
  selector: 'app-cookie-consent',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './cookie-consent.component.html',
  styleUrl: './cookie-consent.component.scss'
})
export class CookieConsentComponent {
  showDetails = signal(false);
  constructor(public consent: ConsentService) {}
}
