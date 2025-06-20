using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Scheduler;
using PolarDrive.WebApi.Services;
using PolarDrive.WebApi.Production;

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

// ✅ SERVIZI MULTI-BRAND
builder.Services.AddHttpClient();
builder.Services.AddScoped<TeslaApiService>();
builder.Services.AddScoped<VehicleApiServiceRegistry>();
builder.Services.AddScoped<VehicleDataService>();

// ✅ SERVIZIO PER GENERAZIONE REPORT MANUALE
builder.Services.AddScoped<IReportGenerationService, ReportGenerationService>();

// ✅ SCHEDULER: Registra PolarDriveScheduler
builder.Services.AddHostedService<PolarDriveScheduler>();

var app = builder.Build();

// Use Swagger only in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
            Console.WriteLine("   - Automatic reports every 5 minutes");
            Console.WriteLine("   - Retry attempts every 1 minute");
            Console.WriteLine("   - Max 5 retries per vehicle");
            Console.WriteLine();
            Console.WriteLine("🔧 API Endpoints available:");
            Console.WriteLine("   - GET  /api/PdfReports - List all reports");
            Console.WriteLine("   - GET  /api/PdfReports/{id}/download - Download report");
            Console.WriteLine("   - POST /api/PdfReports/{id}/regenerate - Regenerate report manually");
            Console.WriteLine("   - PATCH /api/PdfReports/{id}/notes - Update notes");
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
            Console.WriteLine("   - Daily reports at 00:00 UTC");
            Console.WriteLine("   - Weekly reports on Monday at 03:00 UTC");
            Console.WriteLine("   - Monthly reports on 1st at 05:00 UTC");
            Console.WriteLine("   - Retry checks every hour");
            Console.WriteLine("   - Max 5 retries with 5-hour delays");
            Console.WriteLine();
            Console.WriteLine("🔧 API Endpoints:");
            Console.WriteLine("   - POST /api/PdfReports/{id}/regenerate available");
            Console.WriteLine("     (uses IReportGenerationService)");
            Console.WriteLine("===============================");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FATAL ERROR during startup logging: {ex.Message}");
    }
}

app.Run();