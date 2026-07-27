# Invoicely — Angular Frontend

A beautiful, responsive, mobile-friendly Angular 17 (standalone components) frontend for the
Invoices Web App backend (.NET 8 / ASP.NET Core API).

Theme: **black & navy** with electric-blue / cyan accents and gold highlights for money values.

## Features

- **Auth**: Login & Register screens with a split hero/visual panel
- **JWT auth** stored in localStorage, attached via HTTP interceptor, auto-logout on 401
- **Dashboard**: revenue stats, monthly revenue bar chart, recent invoices, quick actions
- **Invoices**: searchable/filterable list, status badges, preview/download PDF, record payments
- **New Invoice**: dynamic line items with expense-name autocomplete, live GST & total calculation
- **Client Ledger**: per-client billing summary and invoice history
- **Reports & GST**: monthly GST chart, breakdown table, export to Excel/PDF
- **Billing**: Premium plan upgrade via Razorpay checkout
- **Admin Console**: platform-wide stats (visible to SuperAdmin role only)
- Fully responsive: collapsible sidebar/hamburger menu on mobile, responsive grids/tables

## Setup

```bash
npm install
```

Update the API base URL in:

- `src/environments/environment.ts`
- `src/environments/environment.prod.ts`

```ts
export const environment = {
  production: false,
  apiUrl: 'https://localhost:7000/api' // <-- point this to your backend
};
```

## Run

```bash
npm start
```

App runs at `http://localhost:4200`.

## Build

```bash
npm run build
```

## Notes on backend mapping

| Screen | Endpoint(s) used |
|---|---|
| Login / Register | `POST /api/auth/login`, `POST /api/auth/register` |
| Dashboard | `GET /api/invoices/dashboard-summary`, `GET /api/invoices` |
| Invoice list | `GET /api/invoices`, `PUT /api/invoices/update-payment/{id}`, `GET /api/invoices/preview-pdf/{id}`, `GET /api/invoices/download-pdf/{id}` |
| New Invoice | `POST /api/invoices/create`, `GET /api/invoices/expense-suggestions` |
| Client Ledger | `GET /api/invoices/client-ledger/{clientId}` |
| Reports | `GET /api/invoices/gst-summary`, `GET /api/invoices/export-fiscal-year`, `GET /api/invoices/export-fiscal-year-pdf` |
| Billing | `POST /api/billing/create-order`, `POST /api/billing/verify-payment` |
| Admin | `GET /api/admin/dashboard` (SuperAdmin role only) |

The JWT is decoded client-side to determine the user's role (`SuperAdmin` shows the Admin nav item)
and to attach the bearer token to every API request.

> Note: `POST /api/invoices/create` is currently restricted to `SuperAdmin` on the backend
> (`[Authorize(Roles = "SuperAdmin")]`) — you may want to relax this to `BusinessOwner` so
> regular users can create invoices from the "New Invoice" screen.
