using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Constants;
using PolarDrive.Data.DbContexts;
using PolarDrive.Data.DTOs;
using PolarDrive.Data.Entities;
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
            // === Company ===
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

            // === Vehicle uniqueness ===
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

            // === Create Vehicle (inactive, awaiting OAuth) ===
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
                VehicleMobileNumber = request.VehicleMobileNumber,
            };
            _dbContext.ClientVehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            await _logger.Info("AdminFullClientInsertController.Post", "New vehicle registered.", $"VIN: {vehicle.Vin}, ClientCompanyId: {company.Id}");
            await _logger.Info("AdminFullClientInsertController.Post", "New client setup pending OAuth", $"VIN: {vehicle.Vin}, ClientCompanyId: {company.Id}");

            // === ZIP presence + extension ===
            if (request.ConsentZip == null || !string.Equals(Path.GetExtension(request.ConsentZip.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                await _logger.Warning("AdminFullClientInsertController.Post", "Uploaded file is not a valid ZIP.");
                await transaction.RollbackAsync();
                return BadRequest(new { errorCode = ErrorCodes.InvalidZipFormat });
            }

            // === Load ZIP to memory ===
            await using var memoryStream = new MemoryStream();
            await request.ConsentZip.CopyToAsync(memoryStream);

            // === Validate ZIP structure ===
            memoryStream.Position = 0;
            try
            {
                using var _ = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: true);
            }
            catch
            {
                await _logger.Warning("AdminFullClientInsertController.Post", "Invalid ZIP payload.");
                await transaction.RollbackAsync();
                return BadRequest(new { errorCode = ErrorCodes.InvalidZipFormat });
            }

            // === Compute SHA-256 hash ===
            memoryStream.Position = 0;
            using var sha256 = SHA256.Create();
            byte[] hashBytes = await sha256.ComputeHashAsync(memoryStream);
            string hash = Convert.ToHexStringLower(hashBytes);

            // === Duplicate check on hash ===
            var existingConsent = await _dbContext.ClientConsents
                .FirstOrDefaultAsync(c => c.ConsentHash == hash);

            if (existingConsent != null)
            {
                await _logger.Warning("AdminFullClientInsertController.Post",
                    "Consent already exists (duplicate hash).",
                    $"Hash: {hash}");
                await transaction.RollbackAsync();
                return BadRequest(new { errorCode = ErrorCodes.DuplicateConsentHash });
            }

            // === Save consent as DB BLOB ===
            memoryStream.Position = 0;
            var consent = new ClientConsent
            {
                ClientCompanyId = company.Id,
                VehicleId = vehicle.Id,
                ConsentType = "Consent Activation",
                UploadDate = DateTime.Now,
                ZipContent = memoryStream.ToArray(), 
                ConsentHash = hash,
                Notes = ""
            };

            _dbContext.ClientConsents.Add(consent);
            await _dbContext.SaveChangesAsync();
            await _logger.Info("AdminFullClientInsertController.Post", "Consent stored as DB BLOB and hash verified.", $"Hash: {hash}");

            // === Commit ===
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
