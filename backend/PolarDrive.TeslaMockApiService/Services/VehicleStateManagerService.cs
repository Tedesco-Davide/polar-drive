using System.Collections.Concurrent;
using System.Text.Json;

namespace PolarDrive.TeslaMockApiService.Services;

/// <summary>
/// Gestisce lo stato persistente di tutti i veicoli simulati
/// </summary>
public class VehicleStateManager
{
    private readonly ILogger<VehicleStateManager> _logger;
    private readonly ConcurrentDictionary<string, VehicleSimulationState> _vehicles = new();
    private readonly string _stateFilePath;

    public VehicleStateManager(ILogger<VehicleStateManager> logger, IConfiguration configuration)
    {
        _logger = logger;
        _stateFilePath = configuration.GetValue<string>("VehicleStateManager:StateFilePath", "vehicle_states.json");
        LoadStateFromFile();
    }

    /// <summary>
    /// Aggiunge o aggiorna lo stato di un veicolo
    /// </summary>
    public void AddOrUpdateVehicle(string vin, VehicleSimulationState state)
    {
        _vehicles.AddOrUpdate(vin, state, (key, oldValue) => state);
        SaveStateToFile(); // Salva automaticamente ad ogni aggiornamento
    }

    /// <summary>
    /// Ottiene lo stato di un veicolo specifico
    /// </summary>
    public VehicleSimulationState? GetVehicle(string vin)
    {
        return _vehicles.TryGetValue(vin, out var state) ? state : null;
    }

    /// <summary>
    /// Ottiene tutti i veicoli
    /// </summary>
    public Dictionary<string, VehicleSimulationState> GetAllVehicles()
    {
        return _vehicles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Verifica se un veicolo esiste
    /// </summary>
    public bool HasVehicle(string vin)
    {
        return _vehicles.ContainsKey(vin);
    }

    /// <summary>
    /// Rimuove un veicolo
    /// </summary>
    public bool RemoveVehicle(string vin)
    {
        var removed = _vehicles.TryRemove(vin, out _);
        if (removed)
        {
            SaveStateToFile();
        }
        return removed;
    }

    /// <summary>
    /// Ottiene il conteggio totale dei veicoli
    /// </summary>
    public int GetVehicleCount()
    {
        return _vehicles.Count;
    }

    /// <summary>
    /// Carica lo stato dal file JSON
    /// </summary>
    private void LoadStateFromFile()
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                var json = File.ReadAllText(_stateFilePath);
                var states = JsonSerializer.Deserialize<Dictionary<string, VehicleSimulationState>>(json);

                if (states != null)
                {
                    foreach (var (vin, state) in states)
                    {
                        _vehicles.TryAdd(vin, state);
                    }

                    _logger.LogInformation("Loaded {Count} vehicle states from {FilePath}",
                        states.Count, _stateFilePath);
                }
            }
            else
            {
                _logger.LogInformation("No existing state file found at {FilePath}. Starting with empty state.",
                    _stateFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading vehicle states from {FilePath}", _stateFilePath);
        }
    }

    /// <summary>
    /// Salva lo stato corrente nel file JSON
    /// </summary>
    private void SaveStateToFile()
    {
        try
        {
            var states = _vehicles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var json = JsonSerializer.Serialize(states, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            File.WriteAllText(_stateFilePath, json);
            _logger.LogDebug("Saved {Count} vehicle states to {FilePath}", states.Count, _stateFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving vehicle states to {FilePath}", _stateFilePath);
        }
    }

    /// <summary>
    /// Forza il salvataggio dello stato (utile per shutdown)
    /// </summary>
    public void ForceSave()
    {
        SaveStateToFile();
        _logger.LogInformation("Force saved all vehicle states");
    }
}