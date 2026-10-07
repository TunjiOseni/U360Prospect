using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using U360Prospect.Models;
using U360Prospect.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace U360Prospect.Controllers;


public class ProspectController : Controller
{
    private readonly ProspectRepository _prospectRepository;

    public ProspectController(ProspectRepository prospectRepository)
    {
        _prospectRepository = prospectRepository;
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
     pageNumber: pageNumber,
     pageSize: pageSize,
         sortColumn: sortColumn,
     sortDirection: sortDirection);

        ViewBag.SearchTerm = searchTerm;
        ViewBag.Country = country;
        ViewBag.Region = region;
        ViewBag.ProspectType = prospectType;
    ViewBag.UbaCustomer = ubaCustomer;
        ViewBag.SortColumn = sortColumn;
        ViewBag.SortDirection = sortDirection;

        ViewBag.Countries =
            await _prospectRepository.GetCountriesAsync();

        ViewBag.Regions =
            await _prospectRepository.GetRegionsAsync(country);

        ViewBag.ProspectTypes =
            await _prospectRepository.GetProspectTypesAsync();

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



     [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertToCustomer(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
             return NotFound();
    }

     try
        {
        await _prospectRepository.ConvertToCustomerAsync(id);

        TempData["UploadMessage"] =
            "Prospect successfully converted to a UBA Customer.";

        return RedirectToAction(nameof(Index));
     }
     catch (Exception ex)
     {
        TempData["UploadMessage"] =
            $"Unable to convert prospect: {ex.Message}";

          return RedirectToAction(nameof(Index));
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
        "Address",
        "State",
        "Region",
        "Phone",
        "Email",
        "Industry",
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
            prospect.Address;

        worksheet.Cell(row + 2, 6).Value =
            prospect.State;

        worksheet.Cell(row + 2, 7).Value =
            prospect.Region;

        worksheet.Cell(row + 2, 8).Value =
            prospect.Phone;

        worksheet.Cell(row + 2, 9).Value =
            prospect.Email;

        worksheet.Cell(row + 2, 10).Value =
            prospect.Industry;

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

    return File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Prospects_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
}

    // =========================================================
    // UPLOAD - GET
    // =========================================================
    [HttpGet]
    public IActionResult Upload()
    {
        return View();
    }


    // =========================================================
    // UPLOAD - POST
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["UploadMessage"] =
                "Please select an Excel file.";

            return RedirectToAction(nameof(Index));
        }


        try
        {
            using var stream = file.OpenReadStream();

            using var workbook =
                new XLWorkbook(stream);

            var worksheet =
                workbook.Worksheets.First();


            var rows =
                worksheet.RangeUsed()
                         ?.RowsUsed()
                         .Skip(1)
                         .ToList();


            if (rows == null || rows.Count == 0)
            {
                TempData["UploadMessage"] =
                    "The Excel file contains no data.";

                return RedirectToAction(nameof(Index));
            }


            int inserted = 0;
            int skipped = 0;


            foreach (var row in rows)
            {
                var prospect = new Prospect
{
    Country =
        row.Cell(1).GetString().Trim(),

    CompanyName =
        row.Cell(2).GetString().Trim(),

    ProspectName =
        row.Cell(3).GetString().Trim(),

    ProspectType =
        row.Cell(4).GetString().Trim(),

    Address =
        row.Cell(5).GetString().Trim(),

    State =
        row.Cell(6).GetString().Trim(),

    Region =
        row.Cell(7).GetString().Trim(),

    Phone =
        row.Cell(8).GetString().Trim(),

    Email =
        row.Cell(9).GetString().Trim(),

    Industry =
        row.Cell(10).GetString().Trim(),

    MarketedBy =
        row.Cell(11).GetString().Trim(),

    MarketedDate =
        row.Cell(12).IsEmpty()
            ? null
            : row.Cell(12).GetDateTime(),

    BranchId =
        row.Cell(13).GetString().Trim(),

    AcquisitionStatus =
        string.IsNullOrWhiteSpace(
            row.Cell(14).GetString())
            ? "New"
            : row.Cell(14).GetString().Trim(),

    Remarks =
        row.Cell(15).GetString().Trim()
};


if (await _prospectRepository.ExistsAsync(
        prospect.Country,
        prospect.CompanyName,
        prospect.ProspectName,
        prospect.BranchId))
{
    skipped++;
    continue;
}


                await _prospectRepository.AddAsync(
                    prospect);

                inserted++;
            }


            TempData["UploadMessage"] =
                $"Upload completed. {inserted} record(s) inserted, {skipped} record(s) skipped.";

        }
        catch (Exception ex)
        {
            TempData["UploadMessage"] =
                $"Upload failed: {ex.Message}";
        }


        return RedirectToAction(nameof(Index));
    }




    // =========================================================
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
