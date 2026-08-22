import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';
import { assistantGuard } from './core/guards/assistant.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./features/landing/landing.component').then(m => m.LandingComponent)
  },
  {
    path: '',
    loadComponent: () =>
      import('./features/auth/auth-shell.component').then(m => m.AuthShellComponent),
    children: [
      { path: 'login', loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent) },
      { path: 'register', loadComponent: () => import('./features/auth/register/register.component').then(m => m.RegisterComponent) },
      { path: 'forgot-password', loadComponent: () => import('./features/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent) },
    ]
  },
  {
    path: 'app',
    canActivate: [authGuard],
    loadComponent: () => import('./shared/components/layout/layout.component').then(m => m.LayoutComponent),
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'invoices', loadComponent: () => import('./features/invoices/invoice-list/invoice-list.component').then(m => m.InvoiceListComponent) },
      { path: 'invoices/new', loadComponent: () => import('./features/invoices/invoice-create/invoice-create.component').then(m => m.InvoiceCreateComponent) },
      { path: 'clients', loadComponent: () => import('./features/clients/client-list/client-list.component').then(m => m.ClientListComponent) },
      { path: 'projects', loadComponent: () => import('./features/projects/projects.component').then(m => m.ProjectsComponent) },
      { path: 'clients/:id/ledger', loadComponent: () => import('./features/clients/client-ledger/client-ledger.component').then(m => m.ClientLedgerComponent) },
      { path: 'reports', loadComponent: () => import('./features/reports/reports.component').then(m => m.ReportsComponent) },
      { path: 'assistants', loadComponent: () => import('./features/assistants/assistants.component').then(m => m.AssistantsComponent) },
      { path: 'bills', loadComponent: () => import('./features/bills/bills.component').then(m => m.BillsComponent) },
      { path: 'notifications', loadComponent: () => import('./features/notifications/notifications.component').then(m => m.NotificationsComponent) },
      { path: 'calendar', loadComponent: () => import('./features/calendar/calendar.component').then(m => m.CalendarComponent) },
      { path: 'branding', loadComponent: () => import('./features/branding/branding.component').then(m => m.BrandingComponent) },
      { path: 'billing', loadComponent: () => import('./features/billing/billing.component').then(m => m.BillingComponent) },
      { path: 'change-password', loadComponent: () => import('./features/profile/change-password.component').then(m => m.ChangePasswordComponent) },
      { path: 'return-approvals', loadComponent: () => import('./features/manager-returns/manager-returns.component').then(m => m.ManagerReturnsComponent) },
      { path: 'admin', canActivate: [adminGuard], loadComponent: () => import('./features/admin/admin.component').then(m => m.AdminComponent) },
    ]
  },
  { path: 'mode', loadComponent: () => import('./features/assistant/mode-picker.component').then(m => m.ModePickerComponent) },
  {
    path: 'assistant',
    canActivate: [assistantGuard],
    loadComponent: () => import('./features/assistant/layout/assistant-layout.component').then(m => m.AssistantLayoutComponent),
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./features/assistant/dashboard/assistant-dashboard.component').then(m => m.AssistantDashboardComponent) },
      { path: 'assignments', loadComponent: () => import('./features/assistant/assignments/assistant-assignments.component').then(m => m.AssistantAssignmentsComponent) },
      { path: 'bills', loadComponent: () => import('./features/assistant/bills/assistant-bills.component').then(m => m.AssistantBillsComponent) },
      { path: 'returns', loadComponent: () => import('./features/assistant/returns/assistant-returns.component').then(m => m.AssistantReturnsComponent) },
      { path: 'change-password', loadComponent: () => import('./features/assistant/change-password/assistant-change-password.component').then(m => m.AssistantChangePasswordComponent) },
    ]
  },
  { path: '**', redirectTo: '' }
];
