using Microsoft.OpenApi.Models;
using PolarDrive.TeslaMockApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ✅ CONFIGURAZIONE SWAGGER CORRETTA
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PolarDrive Tesla Mock API",
        Version = "v1",
        Description = "Mock API per simulare Tesla API durante development"
    });

    // ✅ Gestisci conflitti di nomi se presenti
    c.CustomSchemaIds(type => type.FullName);
});

// ✅ CORS CONFIGURATION
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebAPI", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ✅ Registra tutti i servizi Tesla Mock
builder.Services.AddTeslaMockServices();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ✅ Abilita CORS
app.UseCors("AllowWebAPI");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ✅ Gestisci shutdown gracefully per salvare lo stato
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var stateManager = app.Services.GetRequiredService<VehicleStateManager>();
    stateManager.ForceSave();
});

app.Run();