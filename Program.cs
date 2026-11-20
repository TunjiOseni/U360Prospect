
using U360Prospect.Data;
using U360Prospect.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using U360Prospect.Services;
using U360Prospect.Configuration;
using Microsoft.AspNetCore.DataProtection;
using System.IO;


var builder = WebApplication.CreateBuilder(args);

// Persist ASP.NET Core Data Protection keys
var dataProtectionKeyPath = builder.Environment.IsEnvironment("OfficeTest")
    ? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "U360Prospect",
        "Keys")
    : @"C:\ProgramData\U360Prospect\Keys";

Directory.CreateDirectory(dataProtectionKeyPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName("U360Prospect");

builder.Services
    .Configure<EntrustSettings>(
        builder.Configuration.GetSection("Entrust"));

builder.Services.AddHttpClient<EntrustTokenService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services
    .Configure<ActiveDirectorySettings>(
        builder.Configuration.GetSection("ActiveDirectory"));

builder.Services.AddScoped<
    IActiveDirectoryAuthService,
    ActiveDirectoryAuthService>();

builder.Services.AddSingleton<OracleConnectionFactory>();

builder.Services.AddScoped<ProspectRepository>();
builder.Services.AddScoped<UserProvisioningService>();
builder.Services.AddScoped<AppUserAuthorizationService>();

builder.Services.AddHostedService<InactiveUserBackgroundService>();

// MVC
builder.Services.AddControllersWithViews();
// Swagger API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cookie authentication for the application login page.
// Keep Windows Negotiate available as a separate scheme.
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultSignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
    })
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";

    options.Cookie.Name = ".U360Prospect.Auth";
    options.Cookie.HttpOnly = true;

    // Allow HTTP cookies only during local Development.
    // POC and Production must use HTTPS.
options.Cookie.SecurePolicy =
    builder.Environment.IsDevelopment() ||
    builder.Environment.IsEnvironment("OfficeTest")
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";


        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;

    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = async context =>
        {
            var username = context.Principal?.Identity?.Name;

            if (string.IsNullOrWhiteSpace(username))
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return;
            }

            try
            {
                var accountService = context.HttpContext
                    .RequestServices
                    .GetRequiredService<AppUserAuthorizationService>();

                var isActive = await accountService.IsAccountActiveAsync(
                    username,
                    context.HttpContext.RequestAborted);

                if (!isActive)
                {
                    context.RejectPrincipal();

                    await context.HttpContext.SignOutAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
            catch (OperationCanceledException)
                when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                // The browser cancelled or disconnected the request.
                // Do not invalidate the user's authentication cookie
                // merely because the HTTP request was aborted.
                return;
            }
            catch (Exception ex)

            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();

                logger.LogError(
                    ex,
                    "Cookie validation failed for {Username}.",
                    username);

                // Fail closed if Oracle cannot verify the account.
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }
    };
})


.AddNegotiate()
.AddSaml2("OktaSaml", options =>
{
    options.SignInScheme =
        CookieAuthenticationDefaults.AuthenticationScheme;


   var samlBaseUrl = builder.Environment.IsDevelopment()
    ? "https://localhost:7054"
    : "https://u360prospect.ubagroup.com";

options.SPOptions.EntityId =
    new Sustainsys.Saml2.Metadata.EntityId(
        $"{samlBaseUrl}/Saml2");

options.SPOptions.PublicOrigin =
    new Uri(samlBaseUrl);

    var idp = new Sustainsys.Saml2.IdentityProvider(
        new Sustainsys.Saml2.Metadata.EntityId(
            "http://www.okta.com/exk28d3kx9nzJgHcL1d8"),
        options.SPOptions)
    {
        MetadataLocation =
            "https://staging-ubagroup.okta.com/app/exk28d3kx9nzJgHcL1d8/sso/saml/metadata",

        LoadMetadata = true
    };

    options.IdentityProviders.Add(idp);
});

builder.Services.AddAuthorization();

builder.Services.AddTransient<
    IClaimsTransformation,
    AppRoleService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTP request pipeline
if (!app.Environment.IsDevelopment() &&
!app.Environment.IsEnvironment("OfficeTest"))
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!app.Environment.IsEnvironment("OfficeTest"))
{
    app.UseHttpsRedirection();
}
app.UseRouting();

// Authentication must come before authorization.
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapControllerRoute(
    name: "account",
    pattern: "Account/{action=Login}/{id?}",
    defaults: new { controller = "Account" });

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapGet("/auth-check", (HttpContext context) =>
{
    var identity = context.User.Identity;

    return Results.Json(new
    {
        IsAuthenticated = identity?.IsAuthenticated ?? false,
        AuthenticationType = identity?.AuthenticationType,
        UserName = identity?.Name
    });
})
.RequireAuthorization();


// TEMPORARY: Development-only inactivity audit test.
if (app.Environment.IsDevelopment())
{
    app.MapPost(
        "/dev-tools/run-inactivity-check",
        async (
            AppUserAuthorizationService authorization,
            CancellationToken cancellationToken) =>
        {
            var count =
                await authorization.DisableInactiveUsersAsync(
                    cancellationToken);

            return Results.Ok(new
            {
                Success = true,
                AccountsDisabled = count,
                Message = "Inactive-user check completed."
            });
        })
        .RequireAuthorization(policy =>
            policy.RequireRole("ADMIN"));
}

app.Run();
