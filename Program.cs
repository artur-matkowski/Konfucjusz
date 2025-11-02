using Konfucjusz.Components;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MyConnection")));

// Blazor services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<StudentService>();

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

// Lightweight endpoint to sign in a just-registered user and set the auth cookie.
// This performs a full HTTP response so the Set-Cookie header is sent to the browser.
app.MapGet("/account/signin", async (HttpContext http) =>
{
    var q = http.Request.Query;
    var user = q["username"].ToString();
    var role = q["role"].ToString() ?? string.Empty;

    if (string.IsNullOrWhiteSpace(user))
    {
        return Results.BadRequest("missing username");
    }

    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user),
        new Claim(ClaimTypes.Role, role)
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

    // redirect to home (or return a 200 with a small page)
    return Results.Redirect("/");
});

app.Run();
