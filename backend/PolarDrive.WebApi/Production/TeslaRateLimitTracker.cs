using PolarDrive.Data.DbContexts;

namespace PolarDrive.WebApi.Production
{
    public class TeslaRateLimitTracker
    {
        private readonly Dictionary<long, List<DateTime>> _vehicleRequests = new();
        private readonly Dictionary<long, DateTime> _lastWakeUp = new();
        private readonly TeslaRateLimitConfig _config;
        private readonly PolarDriveLogger _logger;

        public TeslaRateLimitTracker(TeslaRateLimitConfig config, PolarDriveLogger logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task<bool> CanMakeRequestAsync(long vehicleId)
        {
            var now = DateTime.UtcNow;
            var hourAgo = now.AddHours(-1);

            if (!_vehicleRequests.ContainsKey(vehicleId))
                _vehicleRequests[vehicleId] = new List<DateTime>();

            // Rimuovi richieste più vecchie di 1 ora
            _vehicleRequests[vehicleId].RemoveAll(r => r < hourAgo);

            // Controlla limite orario
            if (_vehicleRequests[vehicleId].Count >= _config.RequestsPerHourPerVehicle)
            {
                await _logger.Warning("TeslaRateLimitTracker",
                    $"Rate limit would be exceeded for vehicle {vehicleId}: {_vehicleRequests[vehicleId].Count} requests in last hour");
                return false;
            }

            return true;
        }

        public async Task RecordRequestAsync(long vehicleId)
        {
            if (!_vehicleRequests.ContainsKey(vehicleId))
                _vehicleRequests[vehicleId] = new List<DateTime>();

            _vehicleRequests[vehicleId].Add(DateTime.UtcNow);
        }

        public async Task<bool> CanWakeUpAsync(long vehicleId)
        {
            if (_lastWakeUp.TryGetValue(vehicleId, out var lastWakeUp))
            {
                var timeSinceLastWakeUp = DateTime.UtcNow - lastWakeUp;
                if (timeSinceLastWakeUp.TotalMinutes < 5) // Min 5 minuti tra wake-up
                {
                    await _logger.Info("TeslaRateLimitTracker",
                        $"Skipping wake-up for vehicle {vehicleId} - too soon since last wake-up ({timeSinceLastWakeUp.TotalMinutes:F1} min ago)");
                    return false;
                }
            }
            return true;
        }

        public void RecordWakeUp(long vehicleId)
        {
            _lastWakeUp[vehicleId] = DateTime.UtcNow;
        }

        public async Task WaitForVehicleDelayAsync()
        {
            await Task.Delay(_config.DelayBetweenVehiclesMs);
        }

        public async Task WaitForTokenRefreshDelayAsync()
        {
            await Task.Delay(_config.DelayBetweenTokenRefreshMs);
        }

        public async Task WaitForWakeUpDelayAsync()
        {
            await Task.Delay(_config.WakeUpDelayMs);
        }
    }
}