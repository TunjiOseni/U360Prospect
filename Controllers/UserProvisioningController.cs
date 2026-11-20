using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using U360Prospect.Models.Api;
using U360Prospect.Services;

namespace U360Prospect.Controllers;

[ApiController]
[Route("api/integration/v1/users")]
[Authorize(Roles = "ADMIN")]
public sealed class UserProvisioningController : ControllerBase
{
    private readonly UserProvisioningService _service;
    private readonly ILogger<UserProvisioningController> _logger;

    public UserProvisioningController(
        UserProvisioningService service,
        ILogger<UserProvisioningController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var actor = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(actor))
        {
            return Unauthorized(new
            {
                message = "Authenticated user identity is required."
            });
        }

        try
        {
            var result = await _service.CreateUserAsync(
                request,
                actor,
                cancellationToken);

            if (result == null)
            {
                return Conflict(new
                {
                    message = "The specified username already exists."
                });
            }

            return Created(
                $"/api/integration/v1/users/{Uri.EscapeDataString(result.Username)}",
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "User provisioning failed.");

            return StatusCode(500, new
            {
                message = "An error occurred while provisioning the user."
            });
        }
    }

    [HttpGet("{username}")]
public async Task<IActionResult> GetUser(
    string username,
    CancellationToken cancellationToken)
{
    try
    {
        var result = await _service.GetUserAsync(
            username,
            cancellationToken);

        if (result == null)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        return Ok(result);
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new
        {
            message = ex.Message
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to retrieve user {Username}.",
            username);

        return StatusCode(500, new
        {
            message = "An error occurred while retrieving the user."
        });
    }
}

[HttpPut("{username}")]
public async Task<IActionResult> UpdateUser(
    string username,
    [FromBody] UpdateUserRequest request,
    CancellationToken cancellationToken)
{
    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
    {
        return Unauthorized(new
        {
            message = "Authenticated user identity is required."
        });
    }

    try
    {
        var updated = await _service.UpdateUserAsync(
            username,
            request,
            actor,
            cancellationToken);

        if (!updated)
        {
            return NotFound(new
            {
                message = "User not found."
            });
        }

        return Ok(new
        {
            message = "User details updated successfully.",
            username = username,
            displayName = request.DisplayName.Trim()
        });
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new
        {
            message = ex.Message
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to update user {Username}.",
            username);

        return StatusCode(500, new
        {
            message = "An error occurred while updating the user."
        });
    }
}

[HttpPost("{username}/deactivate")]
public async Task<IActionResult> DeactivateUser(
    string username,
    [FromBody] DeactivateUserRequest request,
    CancellationToken cancellationToken)
{
    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
    {
        return Unauthorized(new
        {
            message = "Authenticated user identity is required."
        });
    }

    try
    {
        var deactivated = await _service.DeactivateUserAsync(
            username,
            request,
            actor,
            cancellationToken);

        if (!deactivated)
        {
            return NotFound(new
            {
                message = "User not found or is already inactive."
            });
        }

        return Ok(new
        {
            message = "User deactivated successfully.",
            username = username,
            isActive = false
        });
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new
        {
            message = ex.Message
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to deactivate user {Username}.",
            username);

        return StatusCode(500, new
        {
            message = "An error occurred while deactivating the user."
        });
    }
}

[HttpPost("{username}/reactivate")]
public async Task<IActionResult> ReactivateUser(
    string username,
    [FromBody] ReactivateUserRequest request,
    CancellationToken cancellationToken)
{
    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
    {
        return Unauthorized(new
        {
            message = "Authenticated user identity is required."
        });
    }

    try
    {
        var reactivated = await _service.ReactivateUserAsync(
            username,
            request,
            actor,
            cancellationToken);

        if (!reactivated)
        {
            return NotFound(new
            {
                message = "User not found or is already active."
            });
        }

        return Ok(new
        {
            message = "User reactivated successfully.",
            username = username,
            isActive = true
        });
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new
        {
            message = ex.Message
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to reactivate user {Username}.",
            username);

        return StatusCode(500, new
        {
            message = "An error occurred while reactivating the user."
        });
    }
}

[HttpPost("{username}/roles")]
public async Task<IActionResult> AssignRole(
    string username,
    [FromBody] AssignRoleRequest request,
    CancellationToken cancellationToken)
{
    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
    {
        return Unauthorized(new
        {
            message = "Authenticated user identity is required."
        });
    }

    try
    {
        var result = await _service.AssignRoleAsync(
            username,
            request,
            actor,
            cancellationToken);

        switch (result)
        {
            case "USER_NOT_FOUND":
                return NotFound(new
                {
                    message = "User not found."
                });

            case "ROLE_NOT_FOUND":
                return BadRequest(new
                {
                    message = "The requested application role does not exist."
                });

            case "ALREADY_ASSIGNED":
                return Conflict(new
                {
                    message = "This role is already assigned to the user."
                });

            case "ASSIGNED":
                return Ok(new
                {
                    message = "Role assigned successfully.",
                    username = username,
                    role = request.RoleName.Trim().ToUpperInvariant()
                });

            default:
                return StatusCode(500, new
                {
                    message = "Unexpected role assignment result."
                });
        }
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new
        {
            message = ex.Message
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to assign role to user {Username}.",
            username);

        return StatusCode(500, new
        {
            message = "An error occurred while assigning the role."
        });
    }
}

[HttpDelete("{username}/roles/{roleName}")]
public async Task<IActionResult> RemoveRole(
    string username,
    string roleName,
    CancellationToken cancellationToken)
{
    var actor = User.Identity?.Name;

    if (string.IsNullOrWhiteSpace(actor))
    {
        return Unauthorized(new
        {
            message = "Authenticated user identity is required."
        });
    }

    try
    {
        var result = await _service.RemoveRoleAsync(
            username,
            roleName,
            actor,
            cancellationToken);

        switch (result)
        {
            case "USER_NOT_FOUND":
                return NotFound(new
                {
                    message = "User not found."
                });

            case "ROLE_NOT_FOUND":
                return BadRequest(new
                {
                    message = "The requested application role does not exist."
                });

            case "ROLE_NOT_ASSIGNED":
                return NotFound(new
                {
                    message = "This role is not assigned to the user."
                });

            case "LAST_ROLE":
                return Conflict(new
                {
                    message = "A user must retain at least one application role."
                });

            case "LAST_ADMIN":
                return Conflict(new
                {
                    message = "The last active ADMIN role cannot be removed."
                });

            case "REMOVED":
                return Ok(new
                {
                    message = "Role removed successfully.",
                    username = username,
                    role = roleName.Trim().ToUpperInvariant()
                });

            default:
                return StatusCode(500, new
                {
                    message = "Unexpected role removal result."
                });
        }
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new
        {
            message = ex.Message
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(
            ex,
            "Failed to remove role from user {Username}.",
            username);

        return StatusCode(500, new
        {
            message = "An error occurred while removing the role."
        });
    }
}

}
