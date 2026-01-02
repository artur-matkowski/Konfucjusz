using Konfucjusz.Components;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Texnomic.Blazor.hCaptcha.Extensions;



var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
// Prefer user-secrets or environment variables for the connection string; appsettings.json should not contain secrets.
var connectionString = builder.Configuration.GetConnectionString("MyConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Missing connection string 'MyConnection'. Configure it via user-secrets or environment variables.\n" +
        "Example (user-secrets): dotnet user-secrets set \"ConnectionStrings:MyConnection\" \"Host=...;Port=5432;Database=...;Username=...;Password=...\"");
}
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<EventService>();
// ParticipantService requires slug secret - fetch from config or environment
var slugSecret = builder.Configuration["SlugSecret"] ?? "default-slug-secret-change-in-production";
builder.Services.AddScoped<ParticipantService>(sp => 
    new ParticipantService(sp.GetRequiredService<ApplicationDbContext>(), slugSecret));
// Bind SMTP settings and register EmailRequest for DI. Put real secrets in user-secrets or environment variables.
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddScoped<EmailRequest>();

// Add hCaptcha services with configuration from environment variables
Console.WriteLine("=== hCaptcha Configuration Debug ===");

// Log all environment variables related to Captcha
var allEnvVars = Environment.GetEnvironmentVariables();
Console.WriteLine("All environment variables containing 'Captcha':");
foreach (System.Collections.DictionaryEntry envVar in allEnvVars)
{
    if (envVar.Key.ToString()?.Contains("Captcha", StringComparison.OrdinalIgnoreCase) == true)
    {
        var value = envVar.Value?.ToString() ?? "(null)";
        var maskedValue = value.Length > 4 ? value.Substring(0, 4) + "..." : "***";
        Console.WriteLine($"  ENV: {envVar.Key} = {maskedValue}");
    }
}

// Try to read from configuration
var siteKeyFromConfig = builder.Configuration["Captcha__hCaptcha__SiteKey"];
var secretFromConfig = builder.Configuration["Captcha__hCaptcha__Secret"];

Console.WriteLine($"Configuration read attempt:");
Console.WriteLine($"  Captcha__hCaptcha__SiteKey from config: {(string.IsNullOrEmpty(siteKeyFromConfig) ? "(null or empty)" : siteKeyFromConfig.Substring(0, Math.Min(4, siteKeyFromConfig.Length)) + "...")}");
Console.WriteLine($"  Captcha__hCaptcha__Secret from config: {(string.IsNullOrEmpty(secretFromConfig) ? "(null or empty)" : secretFromConfig.Substring(0, Math.Min(4, secretFromConfig.Length)) + "...")}");

// Also check alternative configuration paths
var altSiteKey = builder.Configuration["Captcha:hCaptcha:SiteKey"];
var altSecret = builder.Configuration["Captcha:hCaptcha:Secret"];
Console.WriteLine($"Alternative path (colon notation):");
Console.WriteLine($"  Captcha:hCaptcha:SiteKey from config: {(string.IsNullOrEmpty(altSiteKey) ? "(null or empty)" : altSiteKey.Substring(0, Math.Min(4, altSiteKey.Length)) + "...")}");
Console.WriteLine($"  Captcha:hCaptcha:Secret from config: {(string.IsNullOrEmpty(altSecret) ? "(null or empty)" : altSecret.Substring(0, Math.Min(4, altSecret.Length)) + "...")}");

Console.WriteLine("=== End hCaptcha Configuration Debug ===");

builder.Services.AddHttpClient();
builder.Services.AddHCaptcha(options =>
{
    Console.WriteLine("Configuring hCaptcha options...");
    // Use colon notation for Configuration access (ASP.NET Core converts env var __ to :)
    var siteKey = builder.Configuration["Captcha:hCaptcha:SiteKey"];
    var secret = builder.Configuration["Captcha:hCaptcha:Secret"];
    
    if (string.IsNullOrEmpty(siteKey))
    {
        Console.WriteLine("ERROR: SiteKey is null or empty!");
        throw new InvalidOperationException("Missing hCaptcha SiteKey configuration");
    }
    if (string.IsNullOrEmpty(secret))
    {
        Console.WriteLine("ERROR: Secret is null or empty!");
        throw new InvalidOperationException("Missing hCaptcha Secret configuration");
    }
    
    options.SiteKey = siteKey;
    options.Secret = secret;
    Console.WriteLine($"hCaptcha configured successfully - SiteKey: {siteKey.Substring(0, Math.Min(4, siteKey.Length))}..., Secret: {secret.Substring(0, Math.Min(4, secret.Length))}...");
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.Cookie.Name = "KonfucjuszUser";
        opt.LoginPath = "/login";
        opt.Cookie.MaxAge = TimeSpan.FromMinutes(30);
        opt.AccessDeniedPath = "/accessDenied";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB (audio chunks can be large)
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// SignalR hubs
app.MapHub<AudioStreamHub>("/hubs/audio");

// Apply EF migrations and seed initial data on startup (helpful for onboarding).
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        // Backfill slugs for existing events that don't yet have one (after columns exist).
        // This runs once at startup; if slugSecret changes you may wish to regenerate manually.
        var eventsWithoutSlug = db.events.Where(e => e.Slug == null || e.Slug == "").ToList();
        if (eventsWithoutSlug.Any())
        {
            // Retrieve the same slugSecret used for ParticipantService registration above.
            var localSlugSecret = builder.Configuration["SlugSecret"] ?? "default-slug-secret-change-in-production";
            foreach (var ev in eventsWithoutSlug)
            {
                try
                {
                    // Ensure CreationTimestamp has a value; if default(DateTime) set now as fallback.
                    if (ev.CreationTimestamp == default)
                    {
                        ev.CreationTimestamp = DateTime.UtcNow;
                    }
                    var payload = $"{ev.Id}|{ev.CreationTimestamp:O}";
                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(localSlugSecret));
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                    var slug = Convert.ToBase64String(hash)
                        .Replace('+', '-')
                        .Replace('/', '_')
                        .Replace("=", "");
                    if (slug.Length > 16) slug = slug.Substring(0, 16);
                    ev.Slug = slug;
                }
                catch (Exception genEx)
                {
                    Console.WriteLine($"Failed to generate slug for event {ev.Id}: {genEx.Message}");
                }
            }
            db.SaveChanges();
            Console.WriteLine($"Generated slugs for {eventsWithoutSlug.Count} existing events.");
        }
    }
    catch (Exception ex)
    {
        // Log to console; in production you might rethrow or use proper logging.
        Console.WriteLine($"Error applying migrations or seeding database: {ex}");
        throw;
    }
}

// Lightweight endpoint to sign in a just-registered user and set the auth cookie.
// This performs a full HTTP response so the Set-Cookie header is sent to the browser.
app.MapGet("/account/signin", async (HttpContext http) =>
{
    var q = http.Request.Query;
    var user = q["user_email"].ToString();
    var role = q["user_role"].ToString() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(user))
    {
        return Results.BadRequest("missing username");
    }

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user),
    };

    // role may contain multiple roles separated by commas -> add a claim for each role
    if (!string.IsNullOrWhiteSpace(role))
    {
        var roles = role.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var r in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }
    }

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    // redirect to home (or return a 200 with a small page)
    return Results.Redirect("/");
});

app.Run();
