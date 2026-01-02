# Features Index

- **User Registration System**
  - Status: active
  - Description: User registration with password hashing, email verification. Captcha removed (2026-01-02).
  - Files: `Components/Pages/Authentication/Register.razor`, `Schema/UserAccount.cs`

- **Email / SMTP Configuration**
  - Status: in-progress
  - Description: Configure SMTP via OVH instead of MailHog, using environment variables and `SmtpOptions`.
  - Files: `Services/EmailRequest.cs`, `docker-compose.yml`
