import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface BrandingConfig {
  hasBranding: boolean;
  templateStyle: string;
  accentColor: string;
  hasLogo: boolean;
  footerName: string;
  footerTitle: string;
  footerSubtitle: string;
  paymentDetails: string;
  updatedAt?: string;
}

export interface TemplateStyleOption {
  key: string;
  name: string;
  description: string;
}

@Injectable({ providedIn: 'root' })
export class BrandingConfigService {
  private base = `${environment.apiUrl}/invoice-branding`;

  constructor(private http: HttpClient) {}

  getStyles(): Observable<TemplateStyleOption[]> {
    return this.http.get<TemplateStyleOption[]>(`${this.base}/styles`);
  }

  getConfig(): Observable<BrandingConfig> {
    return this.http.get<BrandingConfig>(this.base);
  }

  save(config: {
    templateStyle: string;
    accentColor: string;
    footerName: string;
    footerTitle: string;
    footerSubtitle: string;
    paymentDetails: string;
    logo?: File | null;
  }): Observable<BrandingConfig> {
    const formData = new FormData();
    formData.append('templateStyle', config.templateStyle);
    formData.append('accentColor', config.accentColor);
    formData.append('footerName', config.footerName);
    formData.append('footerTitle', config.footerTitle);
    formData.append('footerSubtitle', config.footerSubtitle);
    formData.append('paymentDetails', config.paymentDetails);
    if (config.logo) formData.append('logo', config.logo, config.logo.name);
    return this.http.post<BrandingConfig>(this.base, formData);
  }

  deleteLogo(): Observable<BrandingConfig> {
    return this.http.delete<BrandingConfig>(`${this.base}/logo`);
  }

  delete(): Observable<any> {
    return this.http.delete(this.base);
  }

  myLogoUrl(): string {
    return `${this.base}/my-logo`;
  }

  getFile(url: string): Observable<Blob> {
    return this.http.get(url, { responseType: 'blob' });
  }
}
