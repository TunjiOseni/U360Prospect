
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using U360Prospect.Services;
using U360Prospect.ViewModels;

namespace U360Prospect.Controllers;

public class AccountController : Controller
{
    private readonly IActiveDirectoryAuthService _activeDirectory;
    private readonly AppUserAuthorizationService _authorization;
    private readonly ILogger<AccountController> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly UserProvisioningService _auditService;


public AccountController(
    IActiveDirectoryAuthService activeDirectory,
    AppUserAuthorizationService authorization,
    UserProvisioningService auditService,
    ILogger<AccountController> logger,
    IHostEnvironment environment,
    IConfiguration configuration)
{
    _activeDirectory = activeDirectory;
    _authorization = authorization;
    _auditService = auditService;
    _logger = logger;
    _environment = environment;
    _configuration = configuration;
}

[AllowAnonymous]
[HttpGet]
public IActionResult Login(string? returnUrl = null)
{
    // Production authentication must go through Okta SAML.
    if (_environment.IsProduction())
    {
        return RedirectToAction(
            nameof(OktaLogin),
            new { returnUrl });
    }

    ViewData["ReturnUrl"] = returnUrl;
    return View("~/Views/Login.cshtml");
}


[AllowAnonymous]
[HttpGet]
public IActionResult OktaLogin(string? returnUrl = null)
{
    // OfficeTest demonstration:
    // Bypass the Okta SAML/Verify redirect and use
    // the existing application login page instead.
    if (_environment.IsEnvironment("OfficeTest"))
    {
        _logger.LogWarning(
            "OFFICETEST: Okta SAML login bypassed. " +
            "Redirecting to application login.");

        return RedirectToAction(
            nameof(Login),
            new { returnUrl });
    }

    // Development and Production retain normal Okta login.
    var properties = new AuthenticationProperties
    {
        RedirectUri = Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action("Index", "Prospect")
    };

    return Challenge(properties, "OktaSaml");
}
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;


// Production authentication must be performed through Okta.
// Reject direct username/password login submissions.
if (_environment.IsProduction())
{
    _logger.LogWarning(
        "Direct username/password login rejected in Production.");

    return RedirectToAction(
        nameof(OktaLogin),
        new { returnUrl });
}

var remoteIp = HttpContext.Connection.RemoteIpAddress;

var isLoopback =
    remoteIp != null &&
    (System.Net.IPAddress.IsLoopback(remoteIp) ||
     (remoteIp.IsIPv4MappedToIPv6 &&
      System.Net.IPAddress.IsLoopback(remoteIp.MapToIPv4())));


var localDevelopmentBypass =
    _environment.IsDevelopment() &&
    _configuration.GetValue<bool>(
        "DevelopmentAuthentication:AllowLocalBypass") &&
    isLoopback;


if (!ModelState.IsValid)
{
    model.Password = string.Empty;

    return View(model);
}

        try
        {
            // Keep the submitted username for AD validation.
            var submittedUsername = model.Username.Trim();

            // Normalize the username for Oracle authorization
            var username = submittedUsername;

            if (username.Contains('\\'))
                username = username.Split('\\').Last();

            if (username.Contains('@'))
                username = username.Split('@')[0];

            // 1. Validate the AD username and password.
// 1. Validate AD credentials, except in explicitly enabled
// localhost-only Development mode.
if (localDevelopmentBypass)
{
    _logger.LogWarning(
        "LOCAL DEVELOPMENT: AD validation skipped for {Username}.",
        username);
}
else
{
    var validCredentials =
        await _activeDirectory.ValidateCredentialsAsync(
            submittedUsername,
            model.Password,
            cancellationToken);


if (!validCredentials)
{
    _logger.LogWarning(
        "Login rejected during Active Directory validation.");

    try
    {
        await _auditService.LogAuditAsync(
            actor: username,
            action: "LOGIN",
            outcome: "FAILURE",
            entityType: "USER",
            entityId: username,
            details: "Active Directory credential validation failed.",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken: cancellationToken);
    }
    catch (Exception auditEx)
    {
        _logger.LogError(
            auditEx,
            "Failed to record unsuccessful login audit for {Username}.",
            username);
    }

    AddLoginError(model);
    return View(model);
}
}



          _logger.LogWarning(
    "Development auth check: Environment={Environment}, AllowLocalBypass={AllowLocalBypass}, RemoteIp={RemoteIp}, IsLoopback={IsLoopback}, LocalDevelopmentBypass={LocalDevelopmentBypass}",
    _environment.EnvironmentName,
    _configuration.GetValue<bool>("DevelopmentAuthentication:AllowLocalBypass"),
    remoteIp,
    isLoopback,
    localDevelopmentBypass);


// 3. Application authorization through Oracle.
// Every login path, including the local Development bypass,
// must have valid application access in Oracle.

var roles = await _authorization.GetRolesAndRecordLoginAsync(
    username,
    cancellationToken);

if (roles.Count == 0)
{
    _logger.LogWarning(
        "Login rejected: Oracle authorization failed for {Username}.",
        username);

    try
    {
        await _auditService.LogAuditAsync(
            actor: username,
            action: "LOGIN",
            outcome: "FAILURE",
            entityType: "USER",
            entityId: username,
            details: "Oracle application authorization failed.",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken: cancellationToken);
    }
    catch (Exception auditEx)
    {
        _logger.LogError(
            auditEx,
            "Failed to record Oracle authorization failure for {Username}.",
            username);
    }

    AddLoginError(model);
    return View(model);
}

// 4. Create the authenticated identity only after all
            // three verification steps have succeeded.
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, username),

                // Prevent AppRoleService from performing a redundant
                // role lookup for this already-verified cookie identity.
                new("AppRolesLoaded", "true")
            };

            foreach (var role in roles.Distinct(
                         StringComparer.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            var properties = new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                properties);

            _logger.LogInformation(
                "Application login succeeded for {Username}.",
                username);

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Prospect");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An unexpected error occurred during application login.");

            AddLoginError(model);
            return View(model);
        }
        finally
        {
            // Never retain the submitted password or token in the model.
            model.Password = string.Empty;
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    private void AddLoginError(LoginViewModel model)
    {
        ModelState.AddModelError(
            string.Empty,
           "Login could not be verified. Check your credentials and confirm that you have application access.");

        model.Password = string.Empty;
      model.TokenCode = string.Empty;
    }
}
