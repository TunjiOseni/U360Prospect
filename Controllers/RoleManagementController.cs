
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using U360Prospect.Services;
using U360Prospect.Models.Api;

namespace U360Prospect.Controllers;

[Authorize(Roles = "ADMIN")]
public class RoleManagementController : Controller
{
    private readonly UserProvisioningService _roleService;

    public RoleManagementController(
        UserProvisioningService roleService)
    {
        _roleService = roleService;
    }

    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var roles = await _roleService.GetAllRolesAsync(
            cancellationToken);

        return View(roles);
    }


    [HttpGet]
public async Task<IActionResult> Details(
    long id,
    CancellationToken cancellationToken)
{
    var roles = await _roleService.GetAllRolesAsync(cancellationToken);

    var role = roles.FirstOrDefault(r => r.RoleId == id);

    if (role == null)
        return NotFound();

    return View(role);
}

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(request);

        var actor = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(actor))
            return Forbid();

        var created = await _roleService.CreateRoleAsync(
            request,
            actor,
            cancellationToken);

        if (!created)
        {
            ModelState.AddModelError(
                "RoleName",
                "This role already exists.");

            return View(request);
        }

        TempData["SuccessMessage"] =
            $"Role {request.RoleName.ToUpperInvariant()} created successfully.";

        return RedirectToAction(nameof(Index));
    }
}
