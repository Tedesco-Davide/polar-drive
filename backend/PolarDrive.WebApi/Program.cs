using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PolarDrive.Data.DbContexts;

var builder = WebApplication.CreateBuilder(args);

// Aggiungi servizi Web API + Swagger
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

// Abilitazione CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Aggiungi il DbContext
builder.Services.AddDbContext<PolarDriveDbContext>(options =>
    options.UseSqlite("Data Source=datapolar.db")
);

var app = builder.Build();

// Usa Swagger solo in sviluppo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
