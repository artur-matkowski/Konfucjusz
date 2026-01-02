# Features Index

- **User Registration System**
  - Status: active
  - Description: User registration with password hashing, email verification, and hCaptcha bot protection. Math captcha replaced with hCaptcha (2026-01-02).
  - Files: `Components/Pages/Authentication/Register.razor`, `Schema/UserAccount.cs`
  - Security: hCaptcha integration using Texnomic.Blazor.hCaptcha package

- **Email / SMTP Configuration**
  - Status: in-progress
  - Description: Configure SMTP via OVH instead of MailHog, using environment variables and `SmtpOptions`.
  - Files: `Services/EmailRequest.cs`, `docker-compose.yml`
