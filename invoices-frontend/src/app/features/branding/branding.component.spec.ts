import { describe, expect, it } from 'vitest';
import { of } from 'rxjs';
import { BrandingComponent } from './branding.component';
import { BrandingConfig } from '../../core/services/branding-config.service';

describe('BrandingComponent payment details defaults', () => {
  function createComponent(savedConfig: BrandingConfig) {
    const brandingService = {
      getStyles: () => of([]),
      getConfig: () => of(savedConfig)
    } as any;
    return new BrandingComponent({} as any, brandingService, {} as any, {} as any);
  }

  it('starts with an empty payment details field', () => {
    const component = createComponent({
      hasBranding: false,
      templateStyle: 'modern',
      accentColor: '#4F7CFF',
      hasLogo: false,
      footerName: '',
      footerTitle: '',
      footerSubtitle: '',
      paymentDetails: ''
    });

    expect(component.config().paymentDetails).toBe('');
  });

  it('preserves previously saved payment details while loading configuration', () => {
    const savedPaymentDetails = 'Bank transfer instructions entered by the account owner';
    const component = createComponent({
      hasBranding: false,
      templateStyle: 'modern',
      accentColor: '#4F7CFF',
      hasLogo: false,
      footerName: '',
      footerTitle: '',
      footerSubtitle: '',
      paymentDetails: savedPaymentDetails
    });

    component.loadDesignConfig();

    expect(component.config().paymentDetails).toBe(savedPaymentDetails);
  });
});
