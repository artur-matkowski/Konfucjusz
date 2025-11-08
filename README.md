# Konfucjusz

A .NET 8 Blazor Server application with PostgreSQL database and email verification workflow.

## Quick Start for Development

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd Konfucjusz
   ```

2. Choose your database setup:

   **Option A: Local PostgreSQL via Docker**
   ```bash
   # Start PostgreSQL and MailHog (SMTP testing server)
   docker compose up -d
   
   # Configure local connection string
   dotnet user-secrets set "ConnectionStrings:MyConnection" "Host=your-host;Port=5432;Database=konfucjusz_db;Username=your-user;Password=your-password"
   ```

   **Option B: External PostgreSQL**
   ```bash
   # Configure your PostgreSQL connection
   dotnet user-secrets set "ConnectionStrings:MyConnection" "Host=your-host;Port=5432;Database=konfucjusz_db;Username=your-user;Password=your-password"
   ```

3. Configure email (choose one):

   **Option A: Local SMTP Testing with MailHog**
   ```bash
   dotnet user-secrets set "Smtp:Host" "localhost"
   dotnet user-secrets set "Smtp:Port" "1025"
   dotnet user-secrets set "Smtp:UseStartTls" "false"
   dotnet user-secrets set "Smtp:Username" ""
   dotnet user-secrets set "Smtp:Password" ""
   dotnet user-secrets set "Smtp:From" "noreply@local.test"
   ```
   Then access MailHog UI at http://localhost:8025

   **Option B: Gmail SMTP** (recommended for production)
   ```bash
   dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
   dotnet user-secrets set "Smtp:Port" "587"
   dotnet user-secrets set "Smtp:UseStartTls" "true"
   dotnet user-secrets set "Smtp:Username" "your-gmail@gmail.com"
   dotnet user-secrets set "Smtp:Password" "your-app-password"  # See Gmail setup below
   dotnet user-secrets set "Smtp:From" "your-gmail@gmail.com"
   ```

4. Run the application:
   ```bash
   dotnet run
   ```
   The app will automatically:
   - Apply any pending database migrations
   - Create a default admin user (if none exists) with:
     - Email: admin@local
     - Password: changeme

## Gmail SMTP Setup

To use Gmail's SMTP server (recommended for production), you need to:

1. Enable 2-Step Verification on your Gmail account:
   - Go to [Google Account Security](https://myaccount.google.com/security)
   - Find "2-Step Verification" and enable it

2. Create an App Password:
   - Go to [App Passwords](https://myaccount.google.com/apppasswords)
   - Select "Mail" as the app
   - Select "Other" as device and name it (e.g., "Konfucjusz")
   - Click "Generate"
   - Copy the 16-character password

3. Configure the application:
   ```bash
   dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
   dotnet user-secrets set "Smtp:Port" "587"
   dotnet user-secrets set "Smtp:UseStartTls" "true"
   dotnet user-secrets set "Smtp:Username" "your-gmail@gmail.com"
   dotnet user-secrets set "Smtp:Password" "your-16-char-app-password"
   dotnet user-secrets set "Smtp:From" "your-gmail@gmail.com"
   ```

## Database Migrations

The application uses EF Core migrations to manage the database schema. Migrations are applied automatically when the application starts.

### Creating a New Migration

After modifying the models (e.g., `Schema/UserAccount.cs`), create a new migration:

```bash
dotnet ef migrations add YourMigrationName
```

### Applying Migrations Manually

Migrations normally apply automatically at startup, but you can apply them manually:

```bash
dotnet ef database update
```

## Docker Development

The included `docker-compose.yml` provides:
- PostgreSQL on port 5433 (to avoid conflicts with host PostgreSQL)
- MailHog for email testing (SMTP on 1025, Web UI on 8025)

```bash
# Start services
docker compose up -d

# View emails in MailHog
open http://localhost:8025

# Stop services
docker compose down

# Stop and remove volumes (resets database)
docker compose down -v
```

## Production Deployment

For production:

1. **Database Connection:**
   - Use environment variables instead of user secrets:
     ```bash
     export ConnectionStrings__MyConnection="Host=prod-db;Port=5432;Database=konfucjusz_db;Username=prod-user;Password=prod-password"
     ```

2. **Email Configuration:**
   - Use Gmail SMTP with an app password (see Gmail setup above)
   - Set via environment variables:
     ```bash
     export Smtp__Host="smtp.gmail.com"
     export Smtp__Port="587"
     export Smtp__UseStartTls="true"
     export Smtp__Username="your-gmail@gmail.com"
     export Smtp__Password="your-app-password"
     export Smtp__From="your-gmail@gmail.com"
     ```

3. **First Run:**
   - The application will create an admin user if none exists
   - Change the default admin password immediately after first login

## Troubleshooting

1. **Email Issues:**
   - Verify SMTP settings in user secrets or environment variables
   - For Gmail: ensure 2FA is enabled and you're using an app password
   - Try MailHog locally to verify your code is sending emails correctly

2. **Database Connection:**
   - Check PostgreSQL is running (`docker compose ps` if using Docker)
   - Verify connection string in user secrets or environment
   - For Docker: ensure you're using port 5433 not 5432

3. **Migration Errors:**
   - Run `dotnet ef database update --verbose` to see detailed errors
   - Check the database exists and the user has CREATE/ALTER permissions

## Security Notes

- Change the default admin password (`changeme`) immediately after first login
- Use environment variables or secure secret management in production
- Keep your Gmail app password secure and rotate it periodically
- Consider using a dedicated email service (SendGrid, Amazon SES) for production