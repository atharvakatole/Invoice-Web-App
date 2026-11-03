# Transactional email setup

The API supports sending OTP and assistant invitation emails through Brevo's HTTPS API. Configure these environment variables on each Render API service:

| Variable | Value |
| --- | --- |
| `Email__Provider` | `Brevo` |
| `Email__ApiKey` | Brevo API key (keep secret) |
| `Email__FromAddress` | Sender address verified in Brevo |
| `Email__FromName` | `Invoicely` |
| `Email__FrontendBaseUrl` | Frontend origin for that environment |

In Brevo, verify the sender address before using it. API key and sender verification are account-specific setup steps. Do not put the API key in source control or frontend configuration.

When `Email__Provider` is set to `Smtp`, the API uses SMTP settings. Render Free web services block outbound SMTP on ports 25, 465, and 587, so production and staging should use the Brevo HTTPS API configuration.
