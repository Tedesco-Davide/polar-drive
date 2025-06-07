using PolarDrive.TeslaMockApiService.Models;

namespace PolarDrive.TeslaMockApiService.Services;

public class MockVehicleDataGenerator
{
    public List<VehicleDto> GenerateVehicleList()
    {
        return
        [
            new()
            {
                // Id = "veh123",
                // Vin = "5YJ3E1EA7KF123456",
                // DisplayName = "Tesla Model 3 (Mock)",
                // State = "online"
            }
        ];
    }
}
