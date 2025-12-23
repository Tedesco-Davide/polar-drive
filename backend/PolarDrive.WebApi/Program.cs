using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PolarDrive.Data.DbContexts;
using PolarDrive.WebApi.Scheduler;
using PolarDrive.WebApi.Services;
using PolarDrive.WebApi.Production;
using Hangfire;
using Hangfire.MemoryStorage;
using PolarDrive.WebApi.Controllers;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using PolarDrive.WebApi.PolarAiReports;
using Microsoft.Data.SqlClient;

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

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MinResponseDataRate = null;
    options.Limits.MaxResponseBufferSize = null;
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
              .AllowAnyMethod()
              .WithExposedHeaders("Content-Disposition");
    });
});

// Setup DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("❌ Connection string 'DefaultConnection' not found. Please set ConnectionStrings__DefaultConnection environment variable.");

builder.Services.AddDbContext<PolarDriveDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        // Timeout 5 minuti per query su SQL server
        sqlOptions.CommandTimeout(300);
    });
});

// Hangfire
builder.Services.AddHangfire(config => config.UseMemoryStorage());
builder.Services.AddHangfireServer(options => new BackgroundJobServerOptions
{
    WorkerCount = 1
});

// LOGGER
builder.Services.AddSingleton<PolarDriveLogger>();

// SERVIZI MULTI-BRAND
builder.Services.AddScoped<TeslaApiService>();
builder.Services.AddScoped<GoogleAdsIntegrationService>();
builder.Services.AddScoped<VehicleApiServiceRegistry>();
builder.Services.AddScoped<VehicleDataService>();

// SERVIZIO GENERAZIONE REPORT
builder.Services.AddScoped<PdfGenerationService>();
builder.Services.AddScoped<IReportGenerationService, ReportGenerationService>();

// SCHEDULER
builder.Services.AddHostedService<PolarDriveScheduler>();
builder.Services.AddHostedService<FileManagerBackgroundService>();

// SERVIZI OUTAGES
builder.Services.AddScoped<IOutageDetectionService, OutageDetectionService>();
builder.Services.AddHostedService<OutageDetectionBackgroundService>();

// SERVIZI SMS
builder.Services.AddScoped<ISmsConfigurationService, SmsService>();
builder.Services.AddScoped<SmsController>();

// SERVIZIO ARCHIVIAZIONE DATI VEICOLI
builder.Services.AddHostedService<VehiclesDataArchiveService>();

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
    var logger = new PolarDriveLogger();

    try
    {

        // Estrai il DB name dalla connection string effettiva
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
        var dbName = connectionStringBuilder.InitialCatalog;
        var serverName = connectionStringBuilder.DataSource;

    await logger.Info("Program.Main",
        "PolarDrive Web API is starting...",
        $"Database: {dbName}");

        await logger.Info("Program.Main",
            "PolarDrive Web API is starting...",
            $"Database: {dbName}");

        // ✅ LOG INFO SUI SERVIZI REGISTRATI
        var vehicleDataService = scope.ServiceProvider.GetRequiredService<VehicleDataService>();
        var stats = await vehicleDataService.GetVehicleCountByBrandAsync();

        await logger.Info("Program.Main", "Vehicle statistics at startup:");
        foreach (var (brand, count) in stats)
        {
            await logger.Info("Program.Main", $"- {brand}: {count} active vehicles");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FATAL ERROR during startup logging: {ex.Message}");
    }
}

app.Run();