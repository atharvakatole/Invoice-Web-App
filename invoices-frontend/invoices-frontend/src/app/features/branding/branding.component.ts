import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { TemplateService, TemplateStatus } from '../../core/services/template.service';
import { BrandingConfigService, BrandingConfig, TemplateStyleOption } from '../../core/services/branding-config.service';
import { ToastService } from '../../core/services/toast.service';

const FIELD_LABELS: Record<string, string> = {
  InvoiceNumber: 'Invoice Number',
  InvoiceDate: 'Invoice Date',
  DueDate: 'Due Date',
  ClientBlock: 'Client / Bill-To Details',
  Table: 'Line Items Table',
  SubTotal: 'Subtotal',
  GSTAmount: 'GST / Tax',
  TotalAmount: 'Total Amount',
  AmountPaid: 'Amount Paid',
  RemainingAmount: 'Balance Due',
  PaymentStatus: 'Payment Status',
  Notes: 'Notes',
};

const DEFAULT_PAYMENT_DETAILS = `BANK – AXIS
ACCOUNT HOLDER – Your Name
A/C NO. – 0000000000000
IFSC – ABCD0000000
BRANCH – Your Branch
UPI ID – yourname@okaxis`;

@Component({
  selector: 'app-branding',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './branding.component.html',
  styleUrl: './branding.component.scss'
})
export class BrandingComponent implements OnInit {
  activeTab = signal<'design' | 'upload'>('design');
  FIELD_LABELS = FIELD_LABELS;

  // ---- Design tab state ----
  styles = signal<TemplateStyleOption[]>([]);
  config = signal<BrandingConfig>({
    hasBranding: false,
    templateStyle: 'modern',
    accentColor: '#4F7CFF',
    hasLogo: false,
    footerName: '',
    footerTitle: '',
    footerSubtitle: '',
    paymentDetails: DEFAULT_PAYMENT_DETAILS,
  });
  loadingConfig = signal(true);
  savingConfig = signal(false);
  logoPreviewUrl = signal<string | null>(null);
  pendingLogoFile: File | null = null;

  // ---- Upload custom PDF tab state ----
  loading = signal(true);
  uploading = signal(false);
  status = signal<TemplateStatus | null>(null);
  previewUrl = signal<SafeResourceUrl | null>(null);
  private previewObjectUrl: string | null = null;
  isDragging = signal(false);

  constructor(
    private templateService: TemplateService,
    private brandingService: BrandingConfigService,
    private toast: ToastService,
    private sanitizer: DomSanitizer
  ) {}

  ngOnInit() {
    this.refresh();
    this.loadDesignConfig();
  }

  // ===================== Design tab =====================

  loadDesignConfig() {
    this.loadingConfig.set(true);
    this.brandingService.getStyles().subscribe(s => this.styles.set(s));
    this.brandingService.getConfig().subscribe({
      next: (c) => {
        this.config.set({
          ...c,
          paymentDetails: c.hasBranding ? c.paymentDetails : DEFAULT_PAYMENT_DETAILS
        });
        this.loadingConfig.set(false);
        if (c.hasLogo) this.loadLogoPreview();
      },
      error: () => this.loadingConfig.set(false)
    });
  }

  loadLogoPreview() {
    this.brandingService.getFile(this.brandingService.myLogoUrl()).subscribe({
      next: (blob) => {
        if (this.logoPreviewUrl()) URL.revokeObjectURL(this.logoPreviewUrl()!);
        this.logoPreviewUrl.set(URL.createObjectURL(blob));
      }
    });
  }

  selectStyle(key: string) {
    this.config.update(c => ({ ...c, templateStyle: key }));
  }

  setFooterName(value: string) {
    this.config.update(c => ({ ...c, footerName: value }));
  }

  setFooterTitle(value: string) {
    this.config.update(c => ({ ...c, footerTitle: value }));
  }

  setFooterSubtitle(value: string) {
    this.config.update(c => ({ ...c, footerSubtitle: value }));
  }

  setPaymentDetails(value: string) {
    this.config.update(c => ({ ...c, paymentDetails: value }));
  }

  onAccentColorChange(value: string) {
    this.config.update(c => ({ ...c, accentColor: value }));
  }

  onLogoSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    if (file.type !== 'image/png' && file.type !== 'image/jpeg') {
      this.toast.error('Logo must be a PNG or JPEG image');
      return;
    }
    if (file.size > 2 * 1024 * 1024) {
      this.toast.error('Logo must be under 2 MB');
      return;
    }

    this.pendingLogoFile = file;
    if (this.logoPreviewUrl()) URL.revokeObjectURL(this.logoPreviewUrl()!);
    this.logoPreviewUrl.set(URL.createObjectURL(file));
    input.value = '';
  }

  removeLogo() {
    this.pendingLogoFile = null;
    if (this.logoPreviewUrl()) {
      URL.revokeObjectURL(this.logoPreviewUrl()!);
      this.logoPreviewUrl.set(null);
    }
    if (this.config().hasLogo) {
      this.brandingService.deleteLogo().subscribe({
        next: (c) => this.config.update(curr => ({ ...curr, hasLogo: c.hasLogo })),
        error: () => this.toast.error('Could not remove logo')
      });
    }
  }

  saveDesign() {
    const c = this.config();
    this.savingConfig.set(true);
    this.brandingService.save({
      templateStyle: c.templateStyle,
      accentColor: c.accentColor,
      footerName: c.footerName,
      footerTitle: c.footerTitle,
      footerSubtitle: c.footerSubtitle,
      paymentDetails: c.paymentDetails,
      logo: this.pendingLogoFile
    }).subscribe({
      next: (saved) => {
        this.savingConfig.set(false);
        this.pendingLogoFile = null;
        this.config.set({ ...saved, paymentDetails: saved.paymentDetails || DEFAULT_PAYMENT_DETAILS });
        this.toast.success('Invoice design saved! New invoices will use this layout.');
      },
      error: (err) => {
        this.savingConfig.set(false);
        this.toast.error(err?.error || 'Could not save invoice design');
      }
    });
  }

  resetDesign() {
    this.brandingService.delete().subscribe({
      next: () => {
        this.config.set({
          hasBranding: false,
          templateStyle: 'modern',
          accentColor: '#4F7CFF',
          hasLogo: false,
          footerName: '',
          footerTitle: '',
          footerSubtitle: '',
          paymentDetails: DEFAULT_PAYMENT_DETAILS,
        });
        if (this.logoPreviewUrl()) {
          URL.revokeObjectURL(this.logoPreviewUrl()!);
          this.logoPreviewUrl.set(null);
        }
        this.toast.success('Reverted to the default invoice layout.');
      },
      error: () => this.toast.error('Could not reset invoice design')
    });
  }

  // ===================== Upload custom PDF tab =====================

  label(key: string): string {
    return FIELD_LABELS[key] ?? key;
  }


  refresh() {
    this.loading.set(true);
    this.templateService.getStatus().subscribe({
      next: (s) => {
        this.status.set(s);
        this.loading.set(false);
        if (s.hasTemplate) this.loadPreview();
        else this.previewUrl.set(null);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error('Could not load invoice template status');
      }
    });
  }

  loadPreview() {
    this.templateService.getFile(this.templateService.previewUrl()).subscribe({
      next: (blob) => {
        if (this.previewObjectUrl) URL.revokeObjectURL(this.previewObjectUrl);
        this.previewObjectUrl = URL.createObjectURL(blob);
        this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.previewObjectUrl));
      },
      error: () => this.toast.error('Could not load template preview')
    });
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.upload(file);
    input.value = '';
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragging.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) this.upload(file);
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDragLeave() {
    this.isDragging.set(false);
  }

  upload(file: File) {
    if (file.type !== 'application/pdf' && !file.name.toLowerCase().endsWith('.pdf')) {
      this.toast.error('Please upload a PDF file');
      return;
    }
    if (file.size > 10 * 1024 * 1024) {
      this.toast.error('File is too large. Maximum size is 10 MB');
      return;
    }

    this.uploading.set(true);
    this.templateService.upload(file).subscribe({
      next: (s) => {
        this.uploading.set(false);
        this.status.set(s);
        this.loadPreview();
        this.toast.success('Invoice template uploaded! Your invoices will now use this layout.');
      },
      error: (err) => {
        this.uploading.set(false);
        this.toast.error(err?.error || 'Could not process this PDF');
      }
    });
  }

  remove() {
    this.templateService.delete().subscribe({
      next: () => {
        this.status.set({ hasTemplate: false, detectedFields: [], missingFields: [] });
        if (this.previewObjectUrl) {
          URL.revokeObjectURL(this.previewObjectUrl);
          this.previewObjectUrl = null;
        }
        this.previewUrl.set(null);
        this.toast.success('Custom template removed. Invoices will use the default layout.');
      },
      error: () => this.toast.error('Could not remove template')
    });
  }
}
