import { Component, OnInit, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { BillingService } from '../../core/services/billing.service';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services/auth.service';

declare const Razorpay: any;

@Component({
  selector: 'app-billing',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './billing.component.html',
  styleUrl: './billing.component.scss'
})
export class BillingComponent implements OnInit {
  processing = signal(false);
  loadingStatus = signal(true);
  premiumAmount = 499;

  subscription = signal<{
    plan: string;
    isPremium: boolean;
    isTrial: boolean;
    isExpired: boolean;
    expiryDate: string;
    nextBillingDate?: string;
    premiumBenefits: string[];
  } | null>(null);

  freeFeatures = [
    'Up to 5 invoices',
    'PDF preview & download',
    'Basic dashboard',
    'Email support'
  ];

  constructor(
    private billingService: BillingService,
    private toast: ToastService,
    public auth: AuthService
  ) {}

  ngOnInit() {
    this.loadStatus();
  }

  loadStatus() {
    this.loadingStatus.set(true);
    this.billingService.getSubscriptionStatus().subscribe({
      next: (status) => {
        this.subscription.set(status);
        this.loadingStatus.set(false);
      },
      error: () => {
        this.loadingStatus.set(false);
      }
    });
  }

  upgrade() {
    this.processing.set(true);
    this.loadRazorpayScript().then(() => {
      this.billingService.createOrder(this.premiumAmount).subscribe({
        next: (order) => {
          const options = {
            key: order.key,
            amount: order.amount,
            currency: order.currency,
            name: 'Invoicely',
            description: 'Premium Plan — 30 days',
            order_id: order.order_id,
            theme: { color: '#4f7cff' },
            handler: (response: any) => {
              this.billingService.verifyPayment({
                razorpayOrderId: response.razorpay_order_id,
                razorpayPaymentId: response.razorpay_payment_id,
                razorpaySignature: response.razorpay_signature
              }).subscribe({
                next: (result) => {
                  this.processing.set(false);
                  this.subscription.update(s => s ? {
                    ...s,
                    plan: 'Premium',
                    isPremium: true,
                    isTrial: false,
                    isExpired: false,
                    nextBillingDate: result.nextBillingDate,
                    expiryDate: result.nextBillingDate
                  } : s);
                  this.toast.success('Welcome to Premium! Your plan is now active.');
                },
                error: () => {
                  this.processing.set(false);
                  this.toast.error('Payment verification failed. Contact support if amount was deducted.');
                }
              });
            },
            modal: {
              ondismiss: () => this.processing.set(false)
            }
          };
          const rzp = new Razorpay(options);
          rzp.on('payment.failed', () => this.processing.set(false));
          rzp.open();
        },
        error: () => {
          this.processing.set(false);
          this.toast.error('Could not initiate payment order');
        }
      });
    }).catch(() => {
      this.processing.set(false);
      this.toast.error('Could not load payment gateway');
    });
  }

  private loadRazorpayScript(): Promise<void> {
    return new Promise((resolve, reject) => {
      if (typeof Razorpay !== 'undefined') return resolve();
      const script = document.createElement('script');
      script.src = 'https://checkout.razorpay.com/v1/checkout.js';
      script.onload = () => resolve();
      script.onerror = () => reject();
      document.body.appendChild(script);
    });
  }
}
