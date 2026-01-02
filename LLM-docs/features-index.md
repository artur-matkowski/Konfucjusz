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

- **Event Slug Generation**
  - Status: active
  - Description: Automatic generation of secure event slugs for enlistment links. Fixed to work in all event creation flows (2026-01-02).
  - Files: `Services/EventService.cs`, `Components/Pages/Events/EventList.razor`, `Components/Pages/Events/EventEdit.razor`
  - Algorithm: HMAC-SHA256 based slug generation with secret key

- **Event Deletion with Recording Cleanup**
  - Status: active
  - Description: Single-event deletion for Organizers with recording file cleanup. Requires typed confirmation.
  - Files: `Components/Pages/Events/EventList.razor`, `Components/Pages/Events/EventEdit.razor`, `Services/EventService.cs`
  - Documentation: `LLM-docs/features/event-deletion-cleanup.md`

- **Administrator Bulk Event Deletion**
  - Status: active
  - Description: Bulk event deletion for Administrators with checkbox selection and simple confirmation. Single-click deletion without typing.
  - Files: `Components/Pages/Admin/EventManagement.razor`, `Services/EventService.cs`
  - Documentation: `LLM-docs/features/admin-bulk-event-deletion.md`
  - Features: Checkbox selection, "Select All", bulk cleanup of recordings, simplified workflow

