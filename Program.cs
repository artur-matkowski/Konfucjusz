using Konfucjusz.Components;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;



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

// Bind captcha options (currently hCaptcha) and register verifier.
builder.Services.Configure<CaptchaOptions>(builder.Configuration.GetSection("Captcha"));
builder.Services.AddHttpClient<ICaptchaVerifier, HcaptchaVerifier>();


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
