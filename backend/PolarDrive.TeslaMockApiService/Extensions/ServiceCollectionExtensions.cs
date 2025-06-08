namespace PolarDrive.TeslaMockApiService.Services;

/// <summary>
/// Extension methods per la registrazione dei servizi nel DI container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra il Tesla Data Pusher e le sue dipendenze
    /// </summary>
    public static IServiceCollection AddTeslaDataPusher(this IServiceCollection services)
    {
        // Registra il background service
        services.AddHostedService<TeslaDataPusherService>();

        // Registra HttpClient per le chiamate HTTP
        services.AddHttpClient();

        return services;
    }

    /// <summary>
    /// Registra tutti i servizi Tesla Mock
    /// </summary>
    public static IServiceCollection AddTeslaMockServices(this IServiceCollection services)
    {
        // Vehicle State Manager (Singleton per mantenere lo stato)
        services.AddSingleton<VehicleStateManager>();

        // Tesla Data Pusher
        services.AddTeslaDataPusher();

        // CORS per permettere chiamate dalla WebAPI
        services.AddCors(options =>
        {
            options.AddPolicy("AllowWebAPI", policy =>
            {
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }
}