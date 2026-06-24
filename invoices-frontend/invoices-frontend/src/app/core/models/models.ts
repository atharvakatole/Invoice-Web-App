export enum PaymentStatus {
  Draft = 0,
  Pending = 1,
  PartiallyPaid = 2,
  Paid = 3,
  Overdue = 4,
  Cancelled = 5
}

export const PaymentStatusLabel: Record<number, string> = {
  0: 'Draft',
  1: 'Pending',
  2: 'Partially Paid',
  3: 'Paid',
  4: 'Overdue',
  5: 'Cancelled'
};

export const PaymentStatusBadge: Record<number, string> = {
  0: 'badge-cancelled',
  1: 'badge-pending',
  2: 'badge-partial',
  3: 'badge-paid',
  4: 'badge-overdue',
  5: 'badge-cancelled'
};

export enum SubscriptionPlan {
  Free = 1,
  Premium = 2,
  Trial = 3
}

export enum UserRole {
  SuperAdmin = 1,
  BusinessOwner = 2,
  Staff = 3
}

export interface Invoice {
  id: string;
  businessId: string;
  clientId: string;
  invoiceNumber: string;
  clientName: string;
  projectName: string;
  invoiceDate: string;
  dueDate: string;
  subTotal: number;
  gstIncluded: boolean;
  gstPercentage: number;
  gstAmount: number;
  totalAmount: number;
  amountPaid: number;
  remainingAmount: number;
  paymentStatus: PaymentStatus;
  invoiceStatus: string;
  notes?: string;
  isClosed: boolean;
  createdAt: string;
}

export interface InvoiceItemRequest {
  expenseName: string;
  projectName: string;
  itemDate: string;
  amount: number;
  quantity: number;
}

export interface CreateInvoiceRequest {
  clientName: string;
  clientEmail: string;
  clientPhone?: string;
  clientAddress?: string;
  dueDate: string;
  gstIncluded: boolean;
  gstPercentage: number;
  notes?: string;
  items: InvoiceItemRequest[];
}

export interface RecentInvoice {
  id: string;
  invoiceNumber: string;
  clientName: string;
  projectName: string;
  invoiceDate: string;
  totalAmount: number;
  paymentStatus: PaymentStatus;
}

export interface SubscriptionStatus {
  plan: string;
  isPremium: boolean;
  isTrial: boolean;
  isExpired: boolean;
  expiryDate: string;
  nextBillingDate?: string;
  premiumBenefits: string[];
}

export interface TopClient {
  clientId: string;
  clientName: string;
  totalBilled: number;
  invoiceCount: number;
}

export interface StatusDistributionEntry {
  status: string;
  count: number;
  total: number;
}

export interface DashboardSummary {
  totalRevenue: number;
  pendingRevenue: number;
  totalInvoices: number;
  paidInvoices: number;
  pendingInvoices: number;
  monthlyRevenue: { month: number; revenue: number }[];
  statusDistribution: StatusDistributionEntry[];
  activeClients: number;
  totalClients: number;
  topClients: TopClient[];
  pendingAssistantPayments: number;
  paidAssistantPayments: number;
  upcomingProjectDays: number;
  recentInvoices: RecentInvoice[];
}

export interface GstSummary {
  totalGSTCollected: number;
  monthlySummary: { month: number; gstCollected: number; revenue: number }[];
}

export interface ClientLedger {
  client: { id: string; clientName: string; clientEmail: string; clientPhone: string };
  summary: { totalInvoices: number; totalBilled: number; totalPaid: number; pendingAmount: number };
  invoices: Invoice[];
}

export interface AdminDashboard {
  totalUsers: number;
  totalBusinesses: number;
  totalInvoices: number;
  totalRevenue: number;
  pendingRevenue: number;
  premiumUsers: number;
}

export interface AuthUser {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  subscriptionPlan: SubscriptionPlan;
}
