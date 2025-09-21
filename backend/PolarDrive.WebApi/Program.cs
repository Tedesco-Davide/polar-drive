using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Scheduler;
using PolarDrive.WebApi.Services;
using PolarDrive.WebApi.Production;
using Hangfire;
using Hangfire.MemoryStorage;
using PolarDrive.Services;
using PolarDrive.WebApi.Controllers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// CONFIGURAZIONE MULTIPART/FORM-DATA
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 100_000_000; // 100MB
    options.MultipartHeadersLengthLimit = int.MaxValue;
    options.BufferBody = true;
    options.BufferBodyLengthLimit = 100_000_000;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 100_000_000; // 100MB
});

// Add Web API services + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PolarDrive.WebApi",
        Version = "v1"
    });
});

builder.Services.AddOptions<OllamaConfig>()
    .Bind(builder.Configuration.GetSection("Ollama"))
    .Validate(cfg =>
        !string.IsNullOrWhiteSpace(cfg.Endpoint) &&
        !string.IsNullOrWhiteSpace(cfg.Model) &&
        cfg.ContextWindow > 0 &&
        cfg.MaxTokens > 0 &&
        cfg.Temperature >= 0 &&
        cfg.TopP > 0 &&
        cfg.TopK > 0 &&
        cfg.RepeatPenalty > 0 &&
        cfg.MaxRetries > 0 &&
        cfg.RetryDelaySeconds > 0,
        "Configurazione Ollama non valida")
    .ValidateOnStart();

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Setup DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=DataPolar_PolarDrive_DB_DEV;Trusted_Connection=true;TrustServerCertificate=true;";

builder.Services.AddDbContext<PolarDriveDbContext>(options =>
    options.UseSqlServer(connectionString)
);

// Hangfire
builder.Services.AddHangfire(config => config.UseMemoryStorage());
builder.Services.AddHangfireServer(options => new BackgroundJobServerOptions
{
    WorkerCount = 1
});

// SERVIZI MULTI-BRAND
builder.Services.AddScoped<TeslaApiService>();
builder.Services.AddScoped<VehicleApiServiceRegistry>();
builder.Services.AddScoped<VehicleDataService>();

// SERVIZIO PER GENERAZIONE REPORT MANUALE
builder.Services.AddScoped<IReportGenerationService, ReportGenerationService>();

// SCHEDULER
builder.Services.AddHostedService<PolarDriveScheduler>();
builder.Services.AddHostedService<FileCleanupService>();

// SERVIZI OUTAGES
builder.Services.AddScoped<IOutageDetectionService, OutageDetectionService>();
builder.Services.AddHostedService<OutageDetectionBackgroundService>();

// SERVIZI SMS / TWILIO
builder.Services.AddScoped<ISmsAdaptiveProfilingService, SmsAdaptiveProfilingService>();
builder.Services.AddScoped<ISmsTwilioConfigurationService, SmsTwilioService>();
builder.Services.AddScoped<SmsAdaptiveProfilingController>();

// Registrazione HttpClientFactory per le chiamate alle API esterne
builder.Services.AddHttpClient();

var app = builder.Build();

// MIDDLEWARE PER MULTIPART (PRIMA di CORS e altri middleware)
app.Use(async (context, next) =>
{
    // Abilita multipart per tutti i Content-Type multipart
    if (context.Request.ContentType?.Contains("multipart/") == true)
    {
        context.Request.EnableBuffering();
    }
    await next();
});

// CREA LE DIRECTORY NECESSARIE PER IL FILE MANAGER, OUTAGES E CONSENTS
var storageBasePath = "storage";
var reportsPath = Path.Combine(storageBasePath, "reports");
var fileManagerZipsPath = Path.Combine(storageBasePath, "filemanager-zips");
var outageZipsPath = Path.Combine(storageBasePath, "outages-zips");
var consentZipsPath = Path.Combine(storageBasePath, "consents-zips");

// Crea le directory se non esistono
Directory.CreateDirectory(storageBasePath);
Directory.CreateDirectory(reportsPath);
Directory.CreateDirectory(fileManagerZipsPath);
Directory.CreateDirectory(outageZipsPath);
Directory.CreateDirectory(consentZipsPath);
Console.WriteLine($"📁 Storage directories created:");
Console.WriteLine($"   - Reports: {Path.GetFullPath(reportsPath)}");
Console.WriteLine($"   - FileManager ZIPs: {Path.GetFullPath(fileManagerZipsPath)}");
Console.WriteLine($"   - Outage ZIPs: {Path.GetFullPath(outageZipsPath)}");
Console.WriteLine($"   - Consent ZIPs: {Path.GetFullPath(consentZipsPath)}");

// Use Swagger only in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard("/hangfire");
}

// Apply middlewares
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapControllers();

// Run app with logger on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PolarDriveDbContext>();
    var logger = new PolarDriveLogger(dbContext);

    try
    {

        var dbName = app.Environment.IsDevelopment()
            ? "DataPolar_PolarDrive_DB_DEV"
            : "DataPolar_PolarDrive_DB_PROD";

        await logger.Info("Program.Main",
            "PolarDrive Web API is starting...",
            $"Database: {dbName}");

        // ✅ LOG INFO SUI SERVIZI REGISTRATI
        var vehicleDataService = scope.ServiceProvider.GetRequiredService<VehicleDataService>();
        var stats = await vehicleDataService.GetVehicleCountByBrandAsync();

        await logger.Info("Program.Main", "Vehicle statistics at startup:");
        foreach (var (brand, count) in stats)
        {
            await logger.Info("Program.Main", $"  - {brand}: {count} active vehicles");
        }

        // 🎯 INFO BASATA SULL'AMBIENTE
        if (app.Environment.IsDevelopment())
        {
            Console.WriteLine();
            Console.WriteLine("=== 🚀 DEVELOPMENT MODE ===");
            Console.WriteLine("📊 PolarDriveScheduler Configuration:");
            Console.WriteLine("   - Automatic reports every 3 minutes");
            Console.WriteLine("   - Retry attempts every 1 minute");
            Console.WriteLine("   - Max 5 retries per vehicle");
            Console.WriteLine();
            Console.WriteLine("🔧 API Endpoints available:");
            Console.WriteLine("   - GET  /api/PdfReports - List all reports");
            Console.WriteLine("   - GET  /api/PdfReports/{id}/download - Download report");
            Console.WriteLine("   - POST /api/PdfReports/{id}/regenerate - Regenerate report manually");
            Console.WriteLine("   - PATCH /api/PdfReports/{id}/notes - Update notes");
            Console.WriteLine();
            Console.WriteLine("📦 File Manager Configuration:");
            Console.WriteLine("   - ZIP files stored in: storage/filemanager-zips/");
            Console.WriteLine("   - Cleanup service runs every 24 hours");
            Console.WriteLine("   - Files older than 30 days are automatically removed");
            Console.WriteLine();
            Console.WriteLine("🔧 File Manager API Endpoints:");
            Console.WriteLine("   - GET  /api/FileManager - List download jobs");
            Console.WriteLine("   - POST /api/FileManager/filemanager-download - Create download request");
            Console.WriteLine("   - GET  /api/FileManager/{id}/download - Download ZIP");
            Console.WriteLine("   - GET  /api/FileManager/available-companies - Get available companies");
            Console.WriteLine("   - GET  /api/FileManager/available-brands - Get available brands");
            Console.WriteLine("   - GET  /api/FileManager/available-vins - Get available VINs");
            Console.WriteLine();
            Console.WriteLine("🔧 Outages API Endpoints:");
            Console.WriteLine("   - GET  /api/OutagePeriods - List all outages");
            Console.WriteLine("   - POST /api/OutagePeriods - Create new outage manually");
            Console.WriteLine("   - POST /api/OutagePeriods/{id}/upload-zip - Upload ZIP to outage");
            Console.WriteLine("   - GET  /api/OutagePeriods/{id}/download-zip - Download outage ZIP");
            Console.WriteLine("   - DELETE /api/OutagePeriods/{id}/delete-zip - Delete outage ZIP");
            Console.WriteLine("   - PATCH /api/OutagePeriods/{id}/resolve - Resolve ongoing outage");
            Console.WriteLine("   - PATCH /api/OutagePeriods/{id}/notes - Update outage notes");
            Console.WriteLine();
            Console.WriteLine("🔧 Client Consents API Endpoints:");
            Console.WriteLine("   - GET  /api/ClientConsents - List all consents");
            Console.WriteLine("   - POST /api/ClientConsents - Create new consent manually");
            Console.WriteLine("   - POST /api/ClientConsents/{id}/upload-zip - Upload ZIP to consent");
            Console.WriteLine("   - GET  /api/ClientConsents/{id}/download - Download consent ZIP");
            Console.WriteLine("   - DELETE /api/ClientConsents/{id}/delete-zip - Delete consent ZIP");
            Console.WriteLine("   - PATCH /api/ClientConsents/{id}/notes - Update consent notes");
            Console.WriteLine("   - GET  /api/ClientConsents/resolve-ids - Resolve company/vehicle IDs");
            Console.WriteLine();
            Console.WriteLine("✅ Upload Configuration:");
            Console.WriteLine("   - Multipart/form-data support enabled");
            Console.WriteLine("   - Max file size: 100MB");
            Console.WriteLine("   - Buffering enabled for large uploads");
            Console.WriteLine();
            Console.WriteLine("📝 Report levels based on monitoring time:");
            Console.WriteLine("   - < 5 min: Valutazione Iniziale");
            Console.WriteLine("   - < 15 min: Analisi Rapida");
            Console.WriteLine("   - < 30 min: Pattern Recognition");
            Console.WriteLine("   - < 60 min: Behavioral Analysis");
            Console.WriteLine("   - > 60 min: Deep Dive Analysis");
            Console.WriteLine();
            Console.WriteLine("📁 Storage Structure:");
            Console.WriteLine("   - storage/reports/ → PDF reports");
            Console.WriteLine("   - storage/filemanager-zips/ → File manager downloads");
            Console.WriteLine("   - storage/outages-zips/ → Outage documentation");
            Console.WriteLine("   - storage/consents-zips/ → Client consent files");
            Console.WriteLine("===============================");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("=== 🏭 PRODUCTION MODE ===");
            Console.WriteLine("📊 PolarDriveScheduler Configuration:");
            Console.WriteLine("   - Monthly reports on 1st at 05:00 UTC");
            Console.WriteLine("   - Retry checks every hour");
            Console.WriteLine("   - Max 5 retries with 5-hour delays");
            Console.WriteLine();
            Console.WriteLine("🔧 API Endpoints:");
            Console.WriteLine("   - POST /api/PdfReports/{id}/regenerate available");
            Console.WriteLine("     (uses IReportGenerationService)");
            Console.WriteLine();
            Console.WriteLine("📦 File Manager:");
            Console.WriteLine("   - PDF ZIP downloads available via /api/FileManager");
            Console.WriteLine("   - Automatic cleanup enabled");
            Console.WriteLine();
            Console.WriteLine("✅ Upload Configuration:");
            Console.WriteLine("   - Production-grade multipart support");
            Console.WriteLine("   - 100MB upload limit configured");
            Console.WriteLine();
            Console.WriteLine("📁 Storage Directories:");
            Console.WriteLine("   - Reports, FileManager, Outages & Consents ZIPs");
            Console.WriteLine("   - Automatic directory creation on startup");
            Console.WriteLine("===============================");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FATAL ERROR during startup logging: {ex.Message}");
    }
}

app.Run();