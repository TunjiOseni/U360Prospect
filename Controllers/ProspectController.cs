using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using U360Prospect.Models;
using U360Prospect.Repositories;
using Microsoft.AspNetCore.Authorization;
using U360Prospect.ViewModels;
using U360Prospect.Services;

namespace U360Prospect.Controllers;

[Authorize]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public class ProspectController : Controller
{
    private readonly ProspectRepository _prospectRepository;

    private readonly ILogger<ProspectController> _logger;

    private readonly UserProvisioningService _auditService;

public ProspectController(
    ProspectRepository prospectRepository,
    UserProvisioningService auditService,
    ILogger<ProspectController> logger)
{
    _prospectRepository = prospectRepository;
    _auditService = auditService;
    _logger = logger;
}


    // =========================================================
    // INDEX
    // =========================================================
    public async Task<IActionResult> Index(
    string? searchTerm,
    string? country,
    string? region,
    string? prospectType,
    string? ubaCustomer,
    string? industry,
    int pageNumber = 1,
    int pageSize = 20,
    string sortColumn = "ProspectName",
    string sortDirection = "asc")

    {
        var result = await _prospectRepository.GetAllAsync(
     searchTerm: searchTerm,
     country: country,
     region: region,
     prospectType: prospectType,
     ubaCustomer: ubaCustomer,
     industry: industry,
     pageNumber: pageNumber,
     pageSize: pageSize,
         sortColumn: sortColumn,
     sortDirection: sortDirection);

        ViewBag.SearchTerm = searchTerm;
        ViewBag.Country = country;
        ViewBag.Region = region;
        ViewBag.ProspectType = prospectType;
        ViewBag.UbaCustomer = ubaCustomer;
	ViewBag.Industry = industry;
        ViewBag.SortColumn = sortColumn;
        ViewBag.SortDirection = sortDirection;

        ViewBag.Countries = CountryCatalog.All
        .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();

        ViewBag.Regions =
            await _prospectRepository.GetRegionsAsync(country);

        ViewBag.ProspectTypes =
            await _prospectRepository.GetProspectTypesAsync();

	ViewBag.Industries =
           await _prospectRepository.GetIndustriesAsync();

        return View(result);
    }


    // =========================================================
    // CREATE - GET
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Countries =
            await _prospectRepository.GetCountriesAsync();

        return View();
    }


    // =========================================================
    // CREATE - POST
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Prospect prospect)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Countries =
                await _prospectRepository.GetCountriesAsync();

            return View(prospect);
        }


        if (await _prospectRepository.ExistsAsync(
        prospect.Country,
        prospect.CompanyName,
        prospect.ProspectName,
        prospect.BranchId))
        {
            ModelState.AddModelError(
                "ProspectName",
                "A prospect with the same country, company, prospect name and branch already exists.");

            ViewBag.Countries =
                await _prospectRepository.GetCountriesAsync();

            return View(prospect);
        }


        await _prospectRepository.AddAsync(prospect);


        TempData["UploadMessage"] =
            "Prospect created successfully.";

        return RedirectToAction(nameof(Index));
    }


    // =========================================================
    // EDIT - GET
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var prospect =
            await _prospectRepository.GetByIdAsync(id);

        if (prospect == null)
            return NotFound();

        ViewBag.Countries =
            await _prospectRepository.GetCountriesAsync();

        return View(prospect);
    }


    // =========================================================
    // EDIT - POST
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        Prospect prospect)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        if (id != prospect.Id)
        {
            prospect.Id = id;
        }


        if (!ModelState.IsValid)
        {
            ViewBag.Countries =
                await _prospectRepository.GetCountriesAsync();

            return View(prospect);
        }


        if (await _prospectRepository.ExistsAsync(
        prospect.Country,
        prospect.CompanyName,
        prospect.ProspectName,
        prospect.BranchId,
        prospect.Id))
        {
            ModelState.AddModelError(
                "ProspectName",
                "Another prospect with the same country, company, prospect name and branch already exists.");

            ViewBag.Countries =
                await _prospectRepository.GetCountriesAsync();

            return View(prospect);
        }


        await _prospectRepository.UpdateAsync(prospect);

        TempData["UploadMessage"] =
            "Prospect updated successfully.";

        return RedirectToAction(nameof(Index));
    }


    // =========================================================
    // DELETE - GET
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var prospect =
            await _prospectRepository.GetByIdAsync(id);

        if (prospect == null)
            return NotFound();

        return View(prospect);
    }




    // =========================================================
    // CONVERT PROSPECT - GET VERIFICATION FORM
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> ConvertToCustomer(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var prospect = await _prospectRepository.GetByIdAsync(id);

        if (prospect == null)
            return NotFound();

        if (string.Equals(
                prospect.UbaCustomer,
                "Yes",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                prospect.AcquisitionStatus,
                "Converted",
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["UploadMessage"] =
                "This prospect has already been converted.";

            return RedirectToAction(nameof(Index));
        }

        var model = new ProspectConversionViewModel
        {
            ProspectId = prospect.Id ?? id,
            ProspectName = prospect.ProspectName,
            ProspectCountry = prospect.Country,
            UploadedDate = prospect.UploadedDate,
            Country = prospect.Country
        };

        return View(model);
    }

    // =========================================================
    // CONVERT PROSPECT - POST AND VERIFY ACCOUNT
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertToCustomer(
        ProspectConversionViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ProspectId))
            return NotFound();

        // Always reload prospect information from Oracle.
        // Never trust the browser for the original upload date.
        var prospect = await _prospectRepository.GetByIdAsync(
            model.ProspectId);

        if (prospect == null)
            return NotFound();

        model.ProspectName = prospect.ProspectName;
        model.ProspectCountry = prospect.Country;
        model.UploadedDate = prospect.UploadedDate;

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var error = await _prospectRepository
                .VerifyAndConvertToCustomerAsync(
                    model.ProspectId,
                    model.AccountNumber,
                    model.SolId,
                    model.Country,
                    model.AccountOpenDate!.Value,
                    User.Identity?.Name ?? "Unknown");

            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

            TempData["UploadMessage"] =
                "Prospect successfully converted to a UBA Customer " +
                "after account verification.";

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Prospect conversion verification failed.");

            ModelState.AddModelError(
                string.Empty,
                "Account verification could not be completed. " +
                "Please try again or contact support.");

            return View(model);
        }
    }




    // =========================================================
    // DELETE - POST
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        await _prospectRepository.DeleteAsync(id);

        TempData["UploadMessage"] =
            "Prospect deleted successfully.";

        return RedirectToAction(nameof(Index));
    }


    // =========================================================
// DOWNLOAD EXCEL
// =========================================================
[HttpGet]
public async Task<IActionResult> Download(
    string? searchTerm,
    string? country,
    string? region,
    string? prospectType,
    string? ubaCustomer)
{
    var prospects =
        await _prospectRepository.GetAllForDownloadAsync(
            searchTerm: searchTerm,
            country: country,
            region: region,
            prospectType: prospectType,
            ubaCustomer: ubaCustomer);

    using var workbook = new XLWorkbook();

    var worksheet =
        workbook.Worksheets.Add("Prospects");

    var headers = new[]
    {
        "Country",
        "Company Name",
        "Prospect Name",
        "Prospect Type",
	"Industry",
        "Address",
        "State",
        "Region",
        "Phone",
        "Email",
        "UBA Customer",
        "Marketed By",
        "Marketed Date",
        "Branch ID",
        "Acquisition Status",
        "Uploaded Date",
        "Converted Date",
        "Remarks"
    };

    for (int i = 0; i < headers.Length; i++)
    {
        worksheet.Cell(1, i + 1).Value =
            headers[i];

        worksheet.Cell(1, i + 1)
            .Style.Font.Bold = true;
    }

    for (int row = 0; row < prospects.Count; row++)
    {
        var prospect = prospects[row];

        worksheet.Cell(row + 2, 1).Value =
            prospect.Country;

        worksheet.Cell(row + 2, 2).Value =
            prospect.CompanyName;

        worksheet.Cell(row + 2, 3).Value =
            prospect.ProspectName;

        worksheet.Cell(row + 2, 4).Value =
            prospect.ProspectType;

	worksheet.Cell(row + 2, 5).Value =
            prospect.Industry;

        worksheet.Cell(row + 2, 6).Value =
            prospect.Address;

        worksheet.Cell(row + 2, 7).Value =
            prospect.State;

        worksheet.Cell(row + 2, 8).Value =
            prospect.Region;

        worksheet.Cell(row + 2, 9).Value =
            prospect.Phone;

        worksheet.Cell(row + 2, 10).Value =
            prospect.Email;

        worksheet.Cell(row + 2, 11).Value =
            prospect.UbaCustomer;

        worksheet.Cell(row + 2, 12).Value =
            prospect.MarketedBy;

        if (prospect.MarketedDate.HasValue)
        {
            worksheet.Cell(row + 2, 13).Value =
                prospect.MarketedDate.Value;

            worksheet.Cell(row + 2, 13)
                .Style.DateFormat.Format =
                "dd-MMM-yyyy";
        }

        worksheet.Cell(row + 2, 14).Value =
            prospect.BranchId;

        worksheet.Cell(row + 2, 15).Value =
            prospect.AcquisitionStatus;

        if (prospect.UploadedDate.HasValue)
        {
            worksheet.Cell(row + 2, 16).Value =
                prospect.UploadedDate.Value;

            worksheet.Cell(row + 2, 16)
                .Style.DateFormat.Format =
                "dd-MMM-yyyy";
        }

        if (prospect.ConvertedDate.HasValue)
        {
            worksheet.Cell(row + 2, 17).Value =
                prospect.ConvertedDate.Value;

            worksheet.Cell(row + 2, 17)
                .Style.DateFormat.Format =
                "dd-MMM-yyyy";
        }

        worksheet.Cell(row + 2, 18).Value =
            prospect.Remarks;
    }

    worksheet.Columns().AdjustToContents();

    using var stream = new MemoryStream();

    workbook.SaveAs(stream);

    stream.Position = 0;

var fileName =
    $"Prospects_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

await RecordDownloadAuditAsync(
    "SUCCESS",
    $"Exported records: {prospects.Count}.",
    fileName);

return File(
    stream.ToArray(),
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    fileName);
}

    // =========================================================
    // UPLOAD - GET
    // =========================================================
    [HttpGet]
[Authorize(Roles = "ADMIN")]
public IActionResult Upload()
{
    ViewBag.Countries = CountryCatalog.All
        .OrderBy(
            c => c.DisplayName,
            StringComparer.OrdinalIgnoreCase)
        .ToList();

    return View();
}


    // =========================================================
    // =========================================================
    // UPLOAD - POST
    // =========================================================
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(
        IFormFile file,
        string? defaultCountry)
    {

	_logger.LogInformation(
    "UPLOAD POST reached. File={FileName}, Size={FileSize}, Country={Country}",
    file?.FileName,
    file?.Length ?? 0,
    defaultCountry);

        if (file == null || file.Length == 0)
        {
	    await RecordUploadAuditAsync(
            "FAILURE",
            "No Excel file was selected.");

            TempData["UploadMessage"] =
                "Please select an Excel file.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.Equals(
                Path.GetExtension(file.FileName),
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {

	    await RecordUploadAuditAsync(
            "FAILURE",
            "Unsupported file format.",
        file.FileName);
            TempData["UploadMessage"] =
                "Only .xlsx Excel files are supported.";
            return RedirectToAction(nameof(Index));
        }

        if (!CountryCatalog.TryNormalize(
                defaultCountry, out var normalizedDefaultCountry))
        {


	    await RecordUploadAuditAsync(
            "FAILURE",
            "Invalid default country selected.",
        file.FileName);
            TempData["UploadMessage"] =
                "Please select a valid default country.";
            return RedirectToAction(nameof(Upload));
        }

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);

            var worksheet = workbook.Worksheets.First();
            var usedRange = worksheet.RangeUsed();

            if (usedRange == null)
            {

                await RecordUploadAuditAsync(
                "FAILURE",
                "The Excel workbook contains no data.",
                file.FileName);

                TempData["UploadMessage"] =
                    "The Excel file contains no data.";
                return RedirectToAction(nameof(Index));
            }

            var allRows = usedRange.RowsUsed().ToList();

            if (allRows.Count < 2)
            {

		await RecordUploadAuditAsync(
               "FAILURE",
               "The Excel workbook contains headers but no data rows.",
               file.FileName);
                TempData["UploadMessage"] =
                    "The Excel file contains headers but no data rows.";
                return RedirectToAction(nameof(Index));
            }

            var headerRow = usedRange.FirstRowUsed()
                ?? throw new InvalidDataException(
                    "The Excel header row could not be read.");

            static string NormalizeHeader(string? value) =>
                new string((value ?? string.Empty)
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToUpperInvariant)
                    .ToArray());

            var headerColumns =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var cell in headerRow.CellsUsed())
            {
                var header = NormalizeHeader(cell.GetString());

                if (string.IsNullOrWhiteSpace(header))
                    continue;

                if (!headerColumns.TryAdd(
                        header, cell.Address.ColumnNumber))
                {
                    throw new InvalidDataException(
                        $"Duplicate Excel header: '{cell.GetString()}'.");
                }
            }

            var requiredHeaders = new[]
            {
                "COMPANYNAME",
                "PROSPECTNAME",
                "PROSPECTTYPE",
                "COUNTRY"
            };

            var missingHeaders = requiredHeaders
                .Where(h => !headerColumns.ContainsKey(h))
                .ToArray();

            if (missingHeaders.Length > 0)
            {
                throw new InvalidDataException(
                    "Missing required Excel column(s): " +
                    string.Join(", ", missingHeaders));
            }

            string ReadText(IXLRangeRow row, string header)
            {
                if (!headerColumns.TryGetValue(
                        header, out var columnNumber))
                    return string.Empty;

                return row.Cell(columnNumber).GetString().Trim();
            }

            DateTime? ReadDate(
                IXLRangeRow row,
                string header,
                int excelRowNumber)
            {
                if (!headerColumns.TryGetValue(
                        header, out var columnNumber))
                    return null;

                var cell = row.Cell(columnNumber);

                if (cell.IsEmpty())
                    return null;

                if (cell.TryGetValue<DateTime>(out var date))
                    return date;

                var text = cell.GetString().Trim();

                if (string.IsNullOrWhiteSpace(text))
                    return null;

                if (DateTime.TryParse(text, out date))
                    return date;

                throw new InvalidDataException(
                    $"Invalid {header} date on Excel row {excelRowNumber}.");
            }

            var prospects = new List<Prospect>();

            foreach (var row in allRows.Skip(1))
            {
                var rowNumber =
                    row.RangeAddress.FirstAddress.RowNumber;

                var companyName = ReadText(row, "COMPANYNAME");
                var prospectName = ReadText(row, "PROSPECTNAME");
                var prospectType = ReadText(row, "PROSPECTTYPE");

                if (string.IsNullOrWhiteSpace(companyName) &&
                    string.IsNullOrWhiteSpace(prospectName) &&
                    string.IsNullOrWhiteSpace(prospectType))
                    continue;

                if (string.IsNullOrWhiteSpace(companyName) ||
                    string.IsNullOrWhiteSpace(prospectName) ||
                    string.IsNullOrWhiteSpace(prospectType))
                {
                    throw new InvalidDataException(
                        $"Company name, prospect name and prospect type " +
                        $"are required on Excel row {rowNumber}.");
                }

                var sourceCountry = ReadText(row, "COUNTRY");
                string countryCode;

                if (string.IsNullOrWhiteSpace(sourceCountry))
                {
                    countryCode = normalizedDefaultCountry;
                }
                else if (!CountryCatalog.TryNormalize(
                             sourceCountry, out countryCode))
                {
                    throw new InvalidDataException(
                        $"Unrecognized country '{sourceCountry}' " +
                        $"on Excel row {rowNumber}. Use an approved " +
                        "country code or country name.");
                }

                var status = ReadText(row, "ACQUISITIONSTATUS");

                prospects.Add(new Prospect
                {
                    Country = countryCode,
                    CompanyName = companyName,
                    ProspectName = prospectName,
                    ProspectType = prospectType,
		    Funded = "No",
                    Address = ReadText(row, "ADDRESS"),
                    State = ReadText(row, "STATE"),
                    Region = ReadText(row, "REGION"),
                    Phone = ReadText(row, "PHONE"),
                    Email = ReadText(row, "EMAIL"),
                    Industry = ReadText(row, "INDUSTRY"),
                    MarketedBy = ReadText(row, "MARKETEDBY"),
                    MarketedDate = ReadDate(row, "MARKETEDDATE", rowNumber),
                    BranchId = ReadText(row, "BRANCHID"),
                    AcquisitionStatus =
                        string.IsNullOrWhiteSpace(status) ? "New" : status,
                    Remarks = ReadText(row, "REMARKS")
                });
            }

            if (prospects.Count == 0)
            {

		await RecordUploadAuditAsync(
                "FAILURE",
                "No valid prospect rows were found.",
                file.FileName);


                TempData["UploadMessage"] =
                    "No valid prospect rows were found in the Excel file.";
                return RedirectToAction(nameof(Index));
            }

            var (inserted, skipped) =
                await _prospectRepository.BulkAddAsync(prospects);

	    await RecordUploadAuditAsync(
    "SUCCESS",
    $"Inserted: {inserted}; Skipped: {skipped}; " +
    $"Default country: {normalizedDefaultCountry}; " +
    $"Rows processed: {prospects.Count}.",
    file.FileName);

            TempData["UploadMessage"] =
                $"Upload completed. {inserted:N0} record(s) inserted; " +
                $"{skipped:N0} duplicate record(s) skipped.";
        }



	catch (InvalidDataException ex)
{
    await RecordUploadAuditAsync(
        "FAILURE",
        $"Excel validation failed: {ex.Message}",
        file?.FileName);

    TempData["UploadMessage"] =
        $"Excel validation failed: {ex.Message}";
}
catch (Exception ex)
{
    _logger.LogError(ex, "Prospect Excel upload failed.");

    await RecordUploadAuditAsync(
        "FAILURE",
        $"Upload processing failed: {ex.GetType().Name}",
        file?.FileName);

    TempData["UploadMessage"] =
        "Upload failed. Check the application log for details.";
}

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
// UPLOAD AUDIT HELPER
// =========================================================
private async Task RecordUploadAuditAsync(
    string outcome,
    string details,
    string? fileName = null)
{
    try
    {
        var actor = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(actor))
            actor = "UNKNOWN";

        var safeFileName = string.IsNullOrWhiteSpace(fileName)
            ? "Not supplied"
            : Path.GetFileName(fileName);

        await _auditService.LogAuditAsync(
            actor: actor,
            action: "UPLOAD",
            outcome: outcome,
            entityType: "PROSPECT_IMPORT",
            entityId: null,
            details: $"File: {safeFileName}. {details}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken: HttpContext.RequestAborted);
    }
    catch (Exception auditEx)
    {
        _logger.LogError(
            auditEx,
            "Failed to record prospect upload audit event.");
    }
}

// =========================================================
// DOWNLOAD AUDIT HELPER
// =========================================================
private async Task RecordDownloadAuditAsync(
    string outcome,
    string details,
    string? fileName = null)
{
    try
    {
        var actor = User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(actor))
            actor = "UNKNOWN";

        var safeFileName = string.IsNullOrWhiteSpace(fileName)
            ? "Not supplied"
            : Path.GetFileName(fileName);

        await _auditService.LogAuditAsync(
            actor: actor,
            action: "DOWNLOAD",
            outcome: outcome,
            entityType: "PROSPECT_EXPORT",
            entityId: null,
            details: $"File: {safeFileName}. {details}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken: HttpContext.RequestAborted);
    }
    catch (Exception auditEx)
    {
        _logger.LogError(
            auditEx,
            "Failed to record prospect download audit event.");
    }
}

    // TEST ORACLE
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> TestOracle()
    {
        try
        {
            var countries =
                await _prospectRepository.GetCountriesAsync();

            return Content(
                $"Oracle connection successful. Countries found: {countries.Count}");
        }
        catch (Exception ex)
        {
            return Content(
                $"Oracle connection failed: {ex.Message}");
        }
    }
}
