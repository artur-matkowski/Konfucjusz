# Development Workflow

- Use `docker compose up` for local/dev deployments.
- Database: Postgres in Docker (`db` service).
- App: `app` service running the Blazor Server app image.
- Mail:
  - Historical: MailHog for dev-only SMTP.
  - In-progress: migrate to OVH SMTP using env-var-based configuration.

- Configuration principles (per `SECURITY-NOTES.md`):
  - No real secrets in source control.
  - Use environment variables or secret stores for DB, SlugSecret, and SMTP.
