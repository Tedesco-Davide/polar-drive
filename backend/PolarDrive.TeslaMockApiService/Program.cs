using PolarDrive.TeslaMockApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ Aggiungi il Tesla Data Pusher
builder.Services.AddTeslaDataPusher();

// ✅ Aggiungi il Mock Vehicle Data Generator (se non già presente)
builder.Services.AddSingleton<MockVehicleDataGenerator>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();