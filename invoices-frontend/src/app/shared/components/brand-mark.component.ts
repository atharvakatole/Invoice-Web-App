import { Component } from '@angular/core';

@Component({
  selector: 'app-brand-mark',
  standalone: true,
  template: `
    <svg viewBox="0 0 48 48" fill="none" focusable="false" aria-hidden="true">
      <rect class="mark-frame" x="3.5" y="3.5" width="41" height="41" rx="11" />
      <path class="mark-paper" d="M16 10.75h11l6 6v19.5a2.75 2.75 0 0 1-2.75 2.75h-14.5A2.75 2.75 0 0 1 13 36.25v-22.75a2.75 2.75 0 0 1 3-2.75Z" />
      <path class="mark-fold" d="M27 11v6h6" />
      <path class="mark-lines" d="M18 22h10M18 26h10M18 30h5" />
      <path class="mark-check" d="m21 31.5 3 3 6-7" />
    </svg>
  `,
  styles: [`
    :host { display: inline-flex; flex: 0 0 auto; }
    svg { display: block; width: 100%; height: 100%; overflow: visible; }
    .mark-frame { stroke: var(--accent); stroke-width: 1.5; }
    .mark-paper { fill: var(--surface); stroke: var(--border-strong); stroke-width: 1.5; stroke-linejoin: round; }
    .mark-fold, .mark-lines { stroke: var(--text); stroke-width: 1.35; stroke-linecap: round; stroke-linejoin: round; opacity: .58; }
    .mark-check { stroke: var(--accent); stroke-width: 2.2; stroke-linecap: round; stroke-linejoin: round; }
  `]
})
export class BrandMarkComponent {}
