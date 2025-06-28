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

var builder = WebApplication.CreateBuilder(args);

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

// Configure DB path
var basePath = AppContext.BaseDirectory;
var relativePath = Path.Combine("..", "..", "..", "..", "PolarDriveInitDB.Cli", "datapolar.db");
var dbPath = Path.GetFullPath(Path.Combine(basePath, relativePath));

// Setup DbContext
builder.Services.AddDbContext<PolarDriveDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}")
);

// Hangfire
var connectionString = $"Data Source={dbPath}";
builder.Services.AddHangfire(config => config.UseMemoryStorage());
builder.Services.AddHangfireServer(options => new BackgroundJobServerOptions
{
    WorkerCount = 1
});

// ✅ SERVIZI MULTI-BRAND
builder.Services.AddScoped<TeslaApiService>();
builder.Services.AddScoped<VehicleApiServiceRegistry>();
builder.Services.AddScoped<VehicleDataService>();

// ✅ SERVIZIO PER GENERAZIONE REPORT MANUALE
builder.Services.AddScoped<IReportGenerationService, ReportGenerationService>();

// ✅ SCHEDULER
builder.Services.AddHostedService<PolarDriveScheduler>();
builder.Services.AddHostedService<FileCleanupService>();

// ✅ SERVIZI OUTAGES
builder.Services.AddScoped<IOutageDetectionService, OutageDetectionService>();
builder.Services.AddHostedService<OutageDetectionBackgroundService>();

// Registrazione HttpClientFactory per le chiamate alle API esterne
builder.Services.AddHttpClient();

var app = builder.Build();

// ✅ CREA LE DIRECTORY NECESSARIE PER IL FILE MANAGER E GLI OUTAGES
var storageBasePath = "storage";
var reportsPath = Path.Combine(storageBasePath, "reports");
var fileManagerZipsPath = Path.Combine(storageBasePath, "filemanager-zips");
var outageZipsPath = Path.Combine(storageBasePath, "outages-zips");

// Crea le directory se non esistono
Directory.CreateDirectory(storageBasePath);
Directory.CreateDirectory(reportsPath);
Directory.CreateDirectory(fileManagerZipsPath);
Directory.CreateDirectory(outageZipsPath);
Console.WriteLine($"📁 Storage directories created:");
Console.WriteLine($"   - Reports: {Path.GetFullPath(reportsPath)}");
Console.WriteLine($"   - FileManager ZIPs: {Path.GetFullPath(fileManagerZipsPath)}");
Console.WriteLine($"   - Outage ZIPs: {Path.GetFullPath(outageZipsPath)}");

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
        await logger.Info("Program.Main", $"PolarDrive Web API is starting...", $"DB Path: {dbPath}");

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
            Console.WriteLine("📝 Report levels based on monitoring time:");
            Console.WriteLine("   - < 5 min: Valutazione Iniziale");
            Console.WriteLine("   - < 15 min: Analisi Rapida");
            Console.WriteLine("   - < 30 min: Pattern Recognition");
            Console.WriteLine("   - < 60 min: Behavioral Analysis");
            Console.WriteLine("   - > 60 min: Deep Dive Analysis");
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
            Console.WriteLine("===============================");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FATAL ERROR during startup logging: {ex.Message}");
    }
}

app.Run();