using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using U360Prospect.Services;

namespace U360Prospect.Controllers;

[Authorize(Roles = "ADMIN")]
public class AuditHistoryController : Controller
{
    private readonly UserProvisioningService _auditService;
    private readonly ILogger<AuditHistoryController> _logger;

    public AuditHistoryController(
        UserProvisioningService auditService,
        ILogger<AuditHistoryController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? actor,
        string? actionFilter,
        string? outcome,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            ModelState.AddModelError(
                string.Empty,
                "The start date cannot be later than the end date.");

            return View(
                await _auditService.GetAuditLogsAsync(
                    cancellationToken: cancellationToken));
        }

        try
        {
            var logs = await _auditService.GetAuditLogsAsync(
                actor,
                actionFilter,
                outcome,
                fromDate?.Date,
                toDate?.Date.AddDays(1),
                cancellationToken);

	    _logger.LogInformation(
    "Audit History retrieved {Count} records.",
    logs.Count);

            ViewBag.Actor = actor;
            ViewBag.Action = actionFilter;
            ViewBag.Outcome = outcome;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve audit history.");

            TempData["ErrorMessage"] =
                "Unable to load audit history. Please try again.";

            return View(new List<U360Prospect.Models.Api.AuditLogResponse>());
        }
    }
}
