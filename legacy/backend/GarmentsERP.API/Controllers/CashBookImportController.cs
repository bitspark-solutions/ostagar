using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GarmentsERP.API.Services;
using System.Globalization;

namespace GarmentsERP.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CashBookImportController : ControllerBase
    {
        private readonly CashBookImportService _importService;
        private readonly ILogger<CashBookImportController> _logger;

        public CashBookImportController(
            CashBookImportService importService,
            ILogger<CashBookImportController> logger)
        {
            _importService = importService;
            _logger = logger;
        }

        /// <summary>
        /// Import Client Cash Book CSV data
        /// </summary>
        [HttpPost("import-csv")]
        public async Task<ActionResult<ImportResult>> ImportCashBookCsv(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("File must be a CSV");
            }

            try
            {
                var importRequest = await ParseCsvFile(csvFile);
                var result = await _importService.ImportCashBookDataAsync(importRequest);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing CSV file: {Message}", ex.Message);
                return StatusCode(500, "Error processing file: " + ex.Message);
            }
        }

        /// <summary>
        /// Import Client Cash Book from manual data entry
        /// </summary>
        [HttpPost("import-manual")]
        public async Task<ActionResult<ImportResult>> ImportCashBookManual([FromBody] CashBookImportRequest request)
        {
            try
            {
                var result = await _importService.ImportCashBookDataAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing manual data: {Message}", ex.Message);
                return StatusCode(500, "Error processing data: " + ex.Message);
            }
        }

        private async Task<CashBookImportRequest> ParseCsvFile(IFormFile csvFile)
        {
            var request = new CashBookImportRequest();
            var transactions = new List<CashBookTransaction>();
            var categories = new HashSet<string>();
            var suppliers = new HashSet<string>();
            var buyers = new HashSet<string>();

            using var stream = new StreamReader(csvFile.OpenReadStream());
            var csvContent = await stream.ReadToEndAsync();
            var lines = csvContent.Split('\n');

            // Skip header rows (first few rows contain titles)
            var dataStartIndex = FindDataStartIndex(lines);

            for (int i = dataStartIndex; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCsvLine(line);
                if (fields.Length < 10) continue;

                var transaction = ParseTransactionFromCsvFields(fields);
                if (transaction != null)
                {
                    transactions.Add(transaction);
                    
                    // Collect categories, suppliers, buyers
                    if (!string.IsNullOrWhiteSpace(transaction.CreditCategory))
                        categories.Add(transaction.CreditCategory);
                    if (!string.IsNullOrWhiteSpace(transaction.DebitCategory))
                        categories.Add(transaction.DebitCategory);
                    if (!string.IsNullOrWhiteSpace(transaction.Supplier))
                        suppliers.Add(transaction.Supplier);
                    if (!string.IsNullOrWhiteSpace(transaction.Buyer))
                        buyers.Add(transaction.Buyer);
                }
            }

            request.Transactions = transactions;
            request.Categories = categories.ToList();
            request.Suppliers = suppliers.ToList();
            request.Buyers = buyers.ToList();

            return request;
        }

        private int FindDataStartIndex(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("Date") && lines[i].Contains("Catagories") && lines[i].Contains("Amount"))
                {
                    return i + 1; // Start from next line after header
                }
            }
            return 3; // Default fallback
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var inQuotes = false;
            var currentField = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.Trim());
                    currentField = "";
                }
                else
                {
                    currentField += c;
                }
            }

            fields.Add(currentField.Trim());
            return fields.ToArray();
        }

        private CashBookTransaction? ParseTransactionFromCsvFields(string[] fields)
        {
            try
            {
                // the client CSV structure:
                // 0: Date, 1: Credit Categories, 2: Credit Particulars, 3: Credit Amount
                // 4: Date, 5: Debit Categories, 6: Supplier, 7: Buyer, 8: Debit Particulars, 9: Debit Amount

                var transaction = new CashBookTransaction();

                // Parse credit side (money in)
                if (!string.IsNullOrWhiteSpace(fields[0]) && !string.IsNullOrWhiteSpace(fields[3]))
                {
                    if (DateTime.TryParseExact(fields[0], "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var creditDate))
                    {
                        transaction.Date = creditDate;
                        transaction.CreditCategory = fields[1]?.Trim() ?? "";
                        transaction.Description = fields[2]?.Trim() ?? "";
                        
                        if (decimal.TryParse(fields[3].Replace(",", "").Replace("\"", "").Trim(), out var creditAmount))
                        {
                            transaction.CreditAmount = creditAmount;
                        }
                    }
                }

                // Parse debit side (money out)
                if (!string.IsNullOrWhiteSpace(fields[4]) && !string.IsNullOrWhiteSpace(fields[9]))
                {
                    if (DateTime.TryParseExact(fields[4], "dd-MM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var debitDate))
                    {
                        // Convert 2-digit year to 4-digit
                        if (debitDate.Year < 2000)
                            debitDate = debitDate.AddYears(100);

                        if (transaction.Date == DateTime.MinValue)
                            transaction.Date = debitDate;

                        transaction.DebitCategory = fields[5]?.Trim() ?? "";
                        transaction.Supplier = fields[6]?.Trim() ?? "";
                        transaction.Buyer = fields[7]?.Trim() ?? "";
                        
                        if (string.IsNullOrWhiteSpace(transaction.Description))
                            transaction.Description = fields[8]?.Trim() ?? "";
                        else if (!string.IsNullOrWhiteSpace(fields[8]))
                            transaction.Description += " - " + fields[8].Trim();

                        if (decimal.TryParse(fields[9].Replace(",", "").Replace("\"", "").Trim(), out var debitAmount))
                        {
                            transaction.DebitAmount = debitAmount;
                        }
                    }
                }

                // Return transaction if we have valid data
                if (transaction.Date != DateTime.MinValue && 
                    (transaction.CreditAmount > 0 || transaction.DebitAmount > 0))
                {
                    return transaction;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing CSV line: {Line}", string.Join(",", fields));
                return null;
            }
        }

        /// <summary>
        /// Get sample CSV format for the client cash book import
        /// </summary>
        [HttpGet("sample-format")]
        public ActionResult GetSampleCsvFormat()
        {
            var sample = new
            {
                format = "Client Cash Book CSV Format",
                structure = new
                {
                    credit_section = new
                    {
                        date = "dd-MM-yyyy format (e.g., 01-02-2025)",
                        categories = "Credit categories (e.g., Loan A/C Chairman, Received: Urbo ltd)",
                        particulars = "Description of transaction",
                        amount = "Amount with commas (e.g., 261,080)"
                    },
                    debit_section = new
                    {
                        date = "dd-MM-yy format (e.g., 01-02-25)",
                        categories = "Debit categories (e.g., Fabric- Purchase, Salary A/C)",
                        supplier = "Supplier name",
                        buyer = "Buyer/Customer name",
                        particulars = "Description of transaction",
                        amount = "Amount with commas (e.g., 2,400)"
                    }
                },
                example_row = "01-02-2025,Loan A/C Chairman,,\"261,080\",01-02-25,Subcontract bill,,Brooklyn: Joggers,,\"2,400\"",
                supported_categories = new
                {
                    credit = new[]
                    {
                        "Loan A/C Chairman",
                        "Received: Urbo ltd",
                        "Received: Brooklyn BD",
                        "Received: Kafit Gallery",
                        "Received: Adl"
                    },
                    debit = new[]
                    {
                        "Fabric- Purchase",
                        "Accessories Bill",
                        "Salary A/C",
                        "Subcontract bill",
                        "Machine- Purchase",
                        "Electric Bill",
                        "Carriage Bill",
                        "Convence",
                        "Tiffin Bill",
                        "Entertainment Bill",
                        "Factory Maintance",
                        "Office Maintance"
                    }
                }
            };

            return Ok(sample);
        }
    }
}
