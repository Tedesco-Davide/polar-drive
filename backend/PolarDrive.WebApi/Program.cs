using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PolarDrive.Data.DbContexts;
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

// ✅ HTTP CLIENT per Tesla API
builder.Services.AddHttpClient();

// === SERVIZI MULTI-BRAND ===
builder.Services.AddScoped<VehicleDataService>();

// Servizi API per i vari brand
builder.Services.AddScoped<TeslaApiService>();
// builder.Services.AddScoped<PolestarApiService>(); // TODO: Implementare
// builder.Services.AddScoped<PorscheApiService>();  // TODO: Implementare

// ✅ REGISTRY per gestire tutti i servizi API
builder.Services.AddScoped<VehicleApiServiceRegistry>();

// Background Service per produzione
builder.Services.AddHostedService<ProductionScheduler>();

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
        await logger.Info("Program.Main", "PolarDrive Web API is starting...", $"DB Path: {dbPath}");

        // ✅ LOG INFO SUI SERVIZI REGISTRATI
        var vehicleDataService = scope.ServiceProvider.GetRequiredService<VehicleDataService>();
        var stats = await vehicleDataService.GetVehicleCountByBrandAsync();

        await logger.Info("Program.Main", "Vehicle statistics at startup:");
        foreach (var (brand, count) in stats)
        {
            await logger.Info("Program.Main", $"  - {brand}: {count} active vehicles");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FATAL ERROR during startup logging: {ex.Message}");
    }
}

app.Run();