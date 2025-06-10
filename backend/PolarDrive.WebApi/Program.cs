using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Fake;
using PolarDrive.WebApi.Production;

var builder = WebApplication.CreateBuilder(args);

// Add Web API services + Swagger
builder.Services.AddControllers();
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

// 🆕 SCHEDULERS: Uno per produzione, uno per development
if (builder.Environment.IsDevelopment())
{
    // Development: Usa FakeProductionScheduler (report ogni 5 minuti)
    builder.Services.AddHostedService<FakeProductionScheduler>();
}
else
{
    // Production: Usa ProductionScheduler normale (report mensili)
    builder.Services.AddHostedService<ProductionScheduler>();
}

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
        var schedulerType = app.Environment.IsDevelopment() ? "FakeProductionScheduler (5min reports)" : "ProductionScheduler (monthly reports)";
        await logger.Info("Program.Main", $"PolarDrive Web API is starting with {schedulerType}...", $"DB Path: {dbPath}");

        // ✅ LOG INFO SUI SERVIZI REGISTRATI
        var vehicleDataService = scope.ServiceProvider.GetRequiredService<VehicleDataService>();
        var stats = await vehicleDataService.GetVehicleCountByBrandAsync();

        await logger.Info("Program.Main", "Vehicle statistics at startup:");
        foreach (var (brand, count) in stats)
        {
            await logger.Info("Program.Main", $"  - {brand}: {count} active vehicles");
        }

        // 🎯 DEVELOPMENT INFO
        if (app.Environment.IsDevelopment())
        {
            Console.WriteLine();
            Console.WriteLine("=== 🚀 DEVELOPMENT MODE ===");
            Console.WriteLine("📊 FakeProductionScheduler: Reports every 5 minutes");
            Console.WriteLine("🔧 Mock API Endpoints available:");
            Console.WriteLine("   - GET  /api/TeslaFakeApi/DataStatus");
            Console.WriteLine("   - POST /api/TeslaFakeApi/GenerateQuickReport");
            Console.WriteLine("   - GET  /api/TeslaFakeApi/ReportStatus");
            Console.WriteLine("   - GET  /api/TeslaFakeApi/DownloadReport/{id}");
            Console.WriteLine();
            Console.WriteLine("⏰ Test sequence:");
            Console.WriteLine("   1. Wait ~7 minutes for first automatic report");
            Console.WriteLine("   2. Or call POST /api/TeslaFakeApi/GenerateQuickReport manually");
            Console.WriteLine("   3. Check GET /api/TeslaFakeApi/ReportStatus for results");
            Console.WriteLine("===============================");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FATAL ERROR during startup logging: {ex.Message}");
    }
}

app.Run();