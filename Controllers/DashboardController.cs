using Microsoft.AspNetCore.Mvc;
using U360Prospect.Repositories;

namespace U360Prospect.Controllers;

public class DashboardController : Controller
{
    private readonly ProspectRepository _prospectRepository;

    public DashboardController(
        ProspectRepository prospectRepository)
    {
        _prospectRepository = prospectRepository;
    }


public async Task<IActionResult> Index(
    string? country = null,
    string? region = null,
    string? branchId = null,
    DateTime? startDate = null,
    DateTime? endDate = null)
{
    var summary =
    await _prospectRepository.GetDashboardSummaryAsync(
        country,
        region,
        branchId,
        startDate,
        endDate);


    var branchPerformance =
    await _prospectRepository.GetBranchPerformanceAsync(
        country,
        region,
        startDate,
        endDate);

    var marketerPerformance =
    await _prospectRepository.GetMarketerPerformanceAsync(
        country,
        region,
        startDate,
        endDate);


ViewBag.MarketerPerformance =
    marketerPerformance;

ViewBag.BranchPerformance = branchPerformance;

    ViewBag.Country = country;
    ViewBag.Region = region;
    ViewBag.BranchId = branchId;
    ViewBag.StartDate = startDate;
    ViewBag.EndDate = endDate;

    ViewBag.Countries =
        await _prospectRepository.GetCountriesAsync();

    ViewBag.Regions =
        await _prospectRepository.GetRegionsAsync(country);

    ViewBag.Branches =
        await _prospectRepository.GetBranchesAsync(
            country,
            region);

    return View(summary);
}
}
