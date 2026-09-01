using Microsoft.AspNetCore.Mvc;
using U360Prospect.Data;
using U360Prospect.Repositories;
using U360Prospect.Models;
using ClosedXML.Excel;


namespace U360Prospect.Controllers;

public class ProspectController : Controller
{
    private readonly OracleConnectionFactory _connectionFactory;
    private readonly ProspectRepository _prospectRepository;

    public ProspectController(
        OracleConnectionFactory connectionFactory,
        ProspectRepository prospectRepository)
    {
        _connectionFactory = connectionFactory;
        _prospectRepository = prospectRepository;
    }


    public async Task<IActionResult> Index(
    string? searchTerm,
    string? region,
    string? companyType,
    int pageNumber = 1,
    string? sortColumn = null,
    string? sortDirection = "asc")
{
    int pageSize = 5;

    var prospects = await _prospectRepository.GetAllAsync(
        searchTerm,
        region,
        companyType,
        pageNumber,
        pageSize,
        sortColumn,
        sortDirection);

    ViewBag.SearchTerm = searchTerm;
    ViewBag.Region = region;
    ViewBag.CompanyType = companyType;
    ViewBag.SortColumn = sortColumn;
    ViewBag.SortDirection = sortDirection;

    var regions = await _prospectRepository.GetRegionsAsync();
    var companyTypes = await _prospectRepository.GetCompanyTypesAsync();

    ViewBag.Regions = regions;
    ViewBag.CompanyTypes = companyTypes;

    return View(prospects);
}

public async Task<IActionResult> Download()
{
    var prospects = await _prospectRepository.GetAllForDownloadAsync();

    using var workbook = new XLWorkbook();

    var worksheet = workbook.Worksheets.Add("Prospects");

    // Headers
    worksheet.Cell(1, 1).Value = "ID";
    worksheet.Cell(1, 2).Value = "Prospect Name";
    worksheet.Cell(1, 3).Value = "Industry";
    worksheet.Cell(1, 4).Value = "Company Type";
    worksheet.Cell(1, 5).Value = "Region";
    worksheet.Cell(1, 6).Value = "Address";
    worksheet.Cell(1, 7).Value = "Email";
    worksheet.Cell(1, 8).Value = "Phone Number";
    worksheet.Cell(1, 9).Value = "Load By";
    worksheet.Cell(1, 10).Value = "Load Date";

    // Data
    int row = 2;

    foreach (var prospect in prospects)
    {
        worksheet.Cell(row, 1).Value = prospect.Id;
        worksheet.Cell(row, 2).Value = prospect.ProspectName;
        worksheet.Cell(row, 3).Value = prospect.Industry;
        worksheet.Cell(row, 4).Value = prospect.CompanyType;
        worksheet.Cell(row, 5).Value = prospect.Region;
        worksheet.Cell(row, 6).Value = prospect.Address ?? "";
        worksheet.Cell(row, 7).Value = prospect.Email ?? "";
        worksheet.Cell(row, 8).Value = prospect.PhoneNumber ?? "";
        worksheet.Cell(row, 9).Value = prospect.LoadBy;

        worksheet.Cell(row, 10).Value =
            prospect.LoadDate?.ToString("dd-MMM-yyyy") ?? "";

        row++;
    }

    // Basic formatting
    var headerRange = worksheet.Range("A1:J1");

    headerRange.Style.Font.Bold = true;

    worksheet.Columns().AdjustToContents();

    // Create Excel file
    using var stream = new MemoryStream();

    workbook.SaveAs(stream);

    stream.Position = 0;

    string fileName =
        $"Prospects_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

    return File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName);
}

[HttpGet]
public IActionResult Upload()
{
    return View();
}

    public IActionResult Create()
{
    return View();
}



[HttpPost]
public async Task<IActionResult> Upload(IFormFile file)
{
    if (file == null || file.Length == 0)
    {
        ModelState.AddModelError("", "Please select an Excel file.");
        return View();
    }

    if (!Path.GetExtension(file.FileName)
        .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
    {
        ModelState.AddModelError("", "Only Excel (.xlsx) files are allowed.");
        return View();
    }

    int totalRows = 0;
    int successCount = 0;
    int duplicateCount = 0;
    int invalidCount = 0;

    using var stream = new MemoryStream();

    await file.CopyToAsync(stream);

    stream.Position = 0;

    using var workbook = new XLWorkbook(stream);

    var worksheet = workbook.Worksheet(1);

    var rows = worksheet.RowsUsed().Skip(1);

    foreach (var row in rows)
    {
        var prospectName = row.Cell(2).GetString().Trim();
        var industry = row.Cell(3).GetString().Trim();
        var companyType = row.Cell(4).GetString().Trim();
        var region = row.Cell(5).GetString().Trim();

        // Skip completely blank rows
        if (string.IsNullOrWhiteSpace(prospectName) &&
            string.IsNullOrWhiteSpace(industry) &&
            string.IsNullOrWhiteSpace(companyType) &&
            string.IsNullOrWhiteSpace(region))
        {
            continue;
        }

        totalRows++;

        // Validate required fields
        if (string.IsNullOrWhiteSpace(prospectName) ||
            string.IsNullOrWhiteSpace(industry) ||
            string.IsNullOrWhiteSpace(companyType) ||
            string.IsNullOrWhiteSpace(region))
        {
            invalidCount++;
            continue;
        }

        // Check duplicate
        bool exists = await _prospectRepository.ExistsAsync(prospectName);

        if (exists)
        {
            duplicateCount++;
            continue;
        }

        var prospect = new Prospect
        {
            ProspectName = prospectName,
            Industry = industry,
            CompanyType = companyType,
            Region = region,
            Address = row.Cell(6).GetString().Trim(),
            Email = row.Cell(7).GetString().Trim(),
            PhoneNumber = row.Cell(8).GetString().Trim(),
            LoadBy = "Excel Upload"
        };

        await _prospectRepository.AddAsync(prospect);

        successCount++;
    }

    TempData["UploadMessage"] =
        $"Upload completed. Total rows processed: {totalRows}. " +
        $"Successfully added: {successCount}. " +
        $"Duplicates skipped: {duplicateCount}. " +
        $"Invalid rows: {invalidCount}.";

    return RedirectToAction(nameof(Index));
}


public async Task<IActionResult> Edit(int id)
{
    var prospect = await _prospectRepository.GetByIdAsync(id);

    if (prospect == null)
    {
        return NotFound();
    }

    return View(prospect);
}

[HttpPost]
public async Task<IActionResult> Edit(Prospect prospect)
{
    if (!ModelState.IsValid)
    {
        return View(prospect);
    }

    await _prospectRepository.UpdateAsync(prospect);

    return RedirectToAction(nameof(Index));
}

    public async Task<IActionResult> TestOracle()
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();

            await connection.OpenAsync();

            return Content("Oracle connection successful!");
        }
        catch (Exception ex)
        {
            return Content($"Oracle connection failed: {ex.Message}");
        }
    }


public async Task<IActionResult> Delete(int id)
{
    var prospect = await _prospectRepository.GetByIdAsync(id);

    if (prospect == null)
    {
        return NotFound();
    }

    return View(prospect);
}

[HttpPost]
public async Task<IActionResult> Delete(Prospect prospect)
{
    await _prospectRepository.DeleteAsync(prospect.Id);

    return RedirectToAction(nameof(Index));
}

[HttpPost]
public async Task<IActionResult> Create(Prospect prospect)
{
    if (!ModelState.IsValid)
    {
        return View(prospect);
    }

    prospect.LoadBy = "U360Prospect";

    await _prospectRepository.AddAsync(prospect);

    return RedirectToAction(nameof(Index));
}

}
