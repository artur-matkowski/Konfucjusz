# Testing Strategy

- **Framework:** xUnit (planned; can be adjusted if you prefer a different framework).
- **Types of tests:**
  - Unit tests for services (e.g. `EventService`, `ParticipantService`).
  - Integration tests for key flows (e.g. user registration + email sending, basic EF operations).
- **Configuration-sensitive behavior** like SMTP will be tested via:
  - Unit tests around validation of `SmtpOptions`.
  - Integration tests using a fake SMTP server or test doubles rather than real OVH.
