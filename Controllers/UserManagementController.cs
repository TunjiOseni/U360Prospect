
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using U360Prospect.Services;
using U360Prospect.Models.Api;

namespace U360Prospect.Controllers;

[Authorize(Roles = "ADMIN")]
public class UserManagementController : Controller
{
    private readonly UserProvisioningService _userService;

    public UserManagementController(UserProvisioningService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var users = await _userService.GetAllUsersAsync(cancellationToken);

        return View(users);
    }

[HttpGet]
public async Task<IActionResult> Edit(
    long id,
    CancellationToken cancellationToken)
{
    var users = await _userService.GetAllUsersAsync(cancellationToken);

    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    ViewBag.UserId = user.UserId;
    ViewBag.Username = user.Username;

    return View(new UpdateUserRequest
    {
        DisplayName = user.DisplayName
    });
}


[HttpGet]
public async Task<IActionResult> Details(
    long id,
    CancellationToken cancellationToken)
{
    var users = await _userService.GetAllUsersAsync(cancellationToken);

    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    var details = await _userService.GetUserAsync(
        user.Username,
        cancellationToken);

    if (details == null)
        return NotFound();

    ViewBag.AvailableRoles =
        await _userService.GetAllRolesAsync(cancellationToken);

    return View(details);
}



[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(
    long id,
    UpdateUserRequest request,
    CancellationToken cancellationToken)
{
    var users = await _userService.GetAllUsersAsync(cancellationToken);

    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    ViewBag.UserId = user.UserId;
    ViewBag.Username = user.Username;

    if (!ModelState.IsValid)
        return View(request);

    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
        return Forbid();

    var updated = await _userService.UpdateUserAsync(
        user.Username,
        request,
        actor,
        cancellationToken);

    if (!updated)
    {
        ModelState.AddModelError(
            string.Empty,
            "Unable to update user details.");

        return View(request);
    }

    TempData["SuccessMessage"] =
        "User details updated successfully.";

    return RedirectToAction(nameof(Details), new { id });
}
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        var actor = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(actor))
        {
            ModelState.AddModelError(
                string.Empty,
                "Unable to identify the administrator creating this user.");

            return View(request);
        }

        var result = await _userService.CreateUserAsync(
            request,
            actor,
            cancellationToken);

        if (result == null)
        {
            ModelState.AddModelError(
                "Username",
                "This username already exists.");

            return View(request);
        }

        TempData["SuccessMessage"] =
            $"User {request.Username} was created successfully.";

        return RedirectToAction(nameof(Index));
    }


[HttpGet]
public async Task<IActionResult> Deactivate(
    long id,
    CancellationToken cancellationToken)
{
    var users = await _userService.GetAllUsersAsync(cancellationToken);
    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    if (!user.IsActive)
        return RedirectToAction(nameof(Details), new { id });

    ViewBag.UserId = user.UserId;
    ViewBag.Username = user.Username;
    ViewBag.DisplayName = user.DisplayName;

    return View(new DeactivateUserRequest());
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Deactivate(
    long id,
    DeactivateUserRequest request,
    CancellationToken cancellationToken)
{
    var users = await _userService.GetAllUsersAsync(cancellationToken);
    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    ViewBag.UserId = user.UserId;
    ViewBag.Username = user.Username;
    ViewBag.DisplayName = user.DisplayName;

    if (!user.IsActive)
        return RedirectToAction(nameof(Details), new { id });

    if (!ModelState.IsValid)
        return View(request);

    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
        return Forbid();

    var result = await _userService.DeactivateUserAsync(
        user.Username,
        request,
        actor,
        cancellationToken);

    if (!result)
    {
        ModelState.AddModelError(
            string.Empty,
            "The account could not be deactivated.");

        return View(request);
    }

    TempData["SuccessMessage"] =
        "User account deactivated successfully.";

    return RedirectToAction(nameof(Details), new { id });
}

[HttpGet]
public async Task<IActionResult> Reactivate(
    long id,
    CancellationToken cancellationToken)
{
    var users = await _userService.GetAllUsersAsync(cancellationToken);
    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    if (user.IsActive)
        return RedirectToAction(nameof(Details), new { id });

    ViewBag.UserId = user.UserId;
    ViewBag.Username = user.Username;
    ViewBag.DisplayName = user.DisplayName;

    return View(new ReactivateUserRequest());
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Reactivate(
    long id,
    ReactivateUserRequest request,
    CancellationToken cancellationToken)
{
    var users = await _userService.GetAllUsersAsync(cancellationToken);
    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    ViewBag.UserId = user.UserId;
    ViewBag.Username = user.Username;
    ViewBag.DisplayName = user.DisplayName;

    if (user.IsActive)
        return RedirectToAction(nameof(Details), new { id });

    if (!ModelState.IsValid)
        return View(request);

    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
        return Forbid();

    var result = await _userService.ReactivateUserAsync(
        user.Username,
        request,
        actor,
        cancellationToken);

    if (!result)
    {
        ModelState.AddModelError(
            string.Empty,
            "The account could not be reactivated.");

        return View(request);
    }

    TempData["SuccessMessage"] =
        "User account reactivated successfully.";

    return RedirectToAction(nameof(Details), new { id });
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AssignRole(
    long id,
    AssignRoleRequest request,
    CancellationToken cancellationToken)
{
    if (!ModelState.IsValid)
    {
        TempData["ErrorMessage"] = "Please select a valid role.";
        return RedirectToAction(nameof(Details), new { id });
    }

    var users = await _userService.GetAllUsersAsync(cancellationToken);
    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
        return Forbid();

    try
    {
        var result = await _userService.AssignRoleAsync(
            user.Username,
            request,
            actor,
            cancellationToken);

        switch (result)
        {
            case "ASSIGNED":
                TempData["SuccessMessage"] =
                    $"Role {request.RoleName} assigned successfully.";
                break;

            case "ALREADY_ASSIGNED":
                TempData["ErrorMessage"] =
                    "This role is already assigned to the user.";
                break;

            case "ROLE_NOT_FOUND":
                TempData["ErrorMessage"] =
                    "The requested role does not exist.";
                break;

            case "USER_NOT_FOUND":
                TempData["ErrorMessage"] =
                    "User not found.";
                break;

            default:
                TempData["ErrorMessage"] =
                    "Unexpected role assignment result.";
                break;
        }
    }
    catch (Exception)
    {
        TempData["ErrorMessage"] =
            "Unable to assign the role. Please contact the administrator.";

        // Log the exception through the application's logger
        // if ILogger is added to this controller.
    }

    return RedirectToAction(nameof(Details), new { id });
}


[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RemoveRole(
    long id,
    string roleName,
    CancellationToken cancellationToken)
{
    var users = await _userService.GetAllUsersAsync(cancellationToken);
    var user = users.FirstOrDefault(u => u.UserId == id);

    if (user == null)
        return NotFound();

    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
        return Forbid();

    try
    {
        var result = await _userService.RemoveRoleAsync(
            user.Username,
            roleName,
            actor,
            cancellationToken);

        switch (result)
        {
            case "REMOVED":
                TempData["SuccessMessage"] =
                    $"Role {roleName} removed successfully.";
                break;

            case "LAST_ADMIN":
                TempData["ErrorMessage"] =
                    "The last active ADMIN role cannot be removed.";
                break;

            case "LAST_ROLE":
                TempData["ErrorMessage"] =
                    "A user must retain at least one application role.";
                break;

            case "ROLE_NOT_ASSIGNED":
                TempData["ErrorMessage"] =
                    "This role is not assigned to the user.";
                break;

            default:
                TempData["ErrorMessage"] =
                    "Unable to remove the requested role.";
                break;
        }
    }
    catch (Exception)
    {
        TempData["ErrorMessage"] =
            "Unable to remove the role. Please try again.";
    }

    return RedirectToAction(nameof(Details), new { id });
}


}
