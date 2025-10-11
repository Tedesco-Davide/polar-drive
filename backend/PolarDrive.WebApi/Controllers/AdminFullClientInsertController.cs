using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Constants;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;
using PolarDrive.WebApi.Helpers;
using System.IO.Compression;
using System.Security.Cryptography;

namespace PolarDrive.WebApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AdminFullClientInsertController(PolarDriveDbContext dbContext, IWebHostEnvironment env) : ControllerBase
{
    private readonly PolarDriveDbContext _dbContext = dbContext;
    private readonly IWebHostEnvironment _env = env;
    private readonly PolarDriveLogger _logger = new(dbContext);

    [HttpPost]
    [RequestSizeLimit(100_000_000)] // ZIP up to 100MB
    public async Task<IActionResult> Post([FromForm] AdminFullClientInsertRequest request)
    {
        await _logger.Info("AdminFullClientInsertController.Post", "Started full client onboarding workflow.");

        using var transaction = await _dbContext.Database.BeginTransactionAsync();

        try
        {
            var company = await _dbContext.ClientCompanies
                .FirstOrDefaultAsync(c => c.VatNumber == request.CompanyVatNumber);

            if (company == null)
            {
                company = new ClientCompany
                {
                    Name = request.CompanyName,
                    VatNumber = request.CompanyVatNumber,

                };
                _dbContext.ClientCompanies.Add(company);
                await _dbContext.SaveChangesAsync();

                await _logger.Info("AdminFullClientInsertController.Post", "Created new client company.", $"VAT: {company.VatNumber}");
            }

            var existingVehicle = await _dbContext.ClientVehicles
                .FirstOrDefaultAsync(v => v.Vin == request.VehicleVIN);

            if (existingVehicle != null)
            {
                var msg = existingVehicle.ClientCompanyId != company.Id
                    ? "Vehicle already associated to a different company."
                    : "Vehicle already associated to the same company.";

                await _logger.Warning("AdminFullClientInsertController.Post", msg, $"VIN: {request.VehicleVIN}");
                var errorCode = existingVehicle.ClientCompanyId != company.Id
                    ? ErrorCodes.VehicleAlreadyAssociatedToAnotherCompany
                    : ErrorCodes.VehicleAlreadyAssociatedToSameCompany;

                await transaction.RollbackAsync();

                return BadRequest(new { errorCode });
            }

            var vehicle = new ClientVehicle
            {
                ClientCompanyId = company.Id,
                Vin = request.VehicleVIN,
                FuelType = request.VehicleFuelType,
                Brand = request.VehicleBrand,
                Model = request.VehicleModel,
                Trim = string.IsNullOrWhiteSpace(request.VehicleTrim) ? null : request.VehicleTrim,
                Color = string.IsNullOrWhiteSpace(request.VehicleColor) ? null : request.VehicleColor,
                IsActiveFlag = false,
                IsFetchingDataFlag = false,
                ClientOAuthAuthorized = false,
                FirstActivationAt = null,
                ReferentName = request.ReferentName,
                ReferentEmail = request.ReferentEmail,
                ReferentMobileNumber = request.ReferentMobile,
            };
            _dbContext.ClientVehicles.Add(vehicle);

            await _dbContext.SaveChangesAsync();
            await _logger.Info("AdminFullClientInsertController.Post", "New vehicle registered.", $"VIN: {vehicle.Vin}, ClientCompanyId: {company.Id}");
            await _logger.Info("AdminFullClientInsertController.Post", "New client setup pending OAuth", $"VIN: {vehicle.Vin}, ClientCompanyId: {company.Id}");

            if (request.ConsentZip == null || !string.Equals(Path.GetExtension(request.ConsentZip.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                await _logger.Warning("AdminFullClientInsertController.Post", "Uploaded file is not a valid ZIP.");
                await transaction.RollbackAsync();
                return BadRequest(new { errorCode = ErrorCodes.InvalidZipFormat });
            }

            await using var memoryStream = new MemoryStream();
            await request.ConsentZip.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Usa storage invece di wwwroot
            var storageBasePath = Path.Combine(Directory.GetCurrentDirectory(), "storage");
            var companiesBasePath = Path.Combine(storageBasePath, "companies"); 
            var companyBasePath = Path.Combine(companiesBasePath, $"company-{company.Id}");
            Directory.CreateDirectory(companyBasePath);

            var consentsDir = Path.Combine(companyBasePath, "consents-zip");
            Directory.CreateDirectory(consentsDir);

            var zipFilename = Path.GetFileNameWithoutExtension(request.ConsentZip.FileName);
            var uniqueName = $"{zipFilename}_{DateTime.Now:ddMMyyyy_HHmmss}.zip";
            var zipPath = Path.Combine(consentsDir, uniqueName);

            memoryStream.Position = 0;
            await using (var fs = new FileStream(zipPath, FileMode.Create))
            {
                await memoryStream.CopyToAsync(fs);
            }

            // Calcola l'hash del file ZIP
            memoryStream.Position = 0; // Reset position
            using var reader = new StreamReader(
                memoryStream,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false), // evita eccezioni su byte non UTF-8
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true
            );
            string zipContent = await reader.ReadToEndAsync();
            string hash = GenericHelpers.ComputeContentHash(zipContent);

            // Controlla se esiste già un consenso con lo stesso hash
            var existingConsent = await _dbContext.ClientConsents
                .FirstOrDefaultAsync(c => c.ConsentHash == hash);

            if (existingConsent != null)
            {
                await _logger.Warning("AdminFullClientInsertController.Post", 
                    "Consent PDF already exists.", 
                    $"Hash: {hash}");
                await transaction.RollbackAsync();
                return BadRequest(new { errorCode = ErrorCodes.DuplicateConsentHash });
            }

            // Crea nuovo consenso
            var consent = new ClientConsent
            {
                ClientCompanyId = company.Id,
                VehicleId = vehicle.Id,
                ConsentType = "Consent Activation",
                UploadDate = DateTime.Now,
                ZipFilePath = zipPath,
                ConsentHash = hash,
                Notes = ""
            };

            _dbContext.ClientConsents.Add(consent);
            await _dbContext.SaveChangesAsync();
            await _logger.Info("AdminFullClientInsertController.Post", "Consent PDF stored and hash verified.", $"Hash: {hash}");

            await transaction.CommitAsync();
            await _logger.Info("AdminFullClientInsertController.Post", "Full client workflow completed successfully.");

            return Ok(new
            {
                message = "Workflow executed successfully!",
                data = new
                {
                    companyId = company.Id,
                    companyName = company.Name,
                    companyVatNumber = company.VatNumber,
                    vehicleId = vehicle.Id,
                    vehicleVin = vehicle.Vin,
                    vehicleBrand = vehicle.Brand,
                    vehicleModel = vehicle.Model
                }
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            await _logger.Error("AdminFullClientInsertController.Post", "Exception during full client workflow.", ex.ToString());
            return StatusCode(500, $"SERVER ERROR → STATUS CODE: Error while executing the Insert Workflow! {ex.Message} ");
        }
    }
}
