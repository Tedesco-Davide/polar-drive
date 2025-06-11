using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PolarDrive.WebApi.PolarAiReports;

public static class RawDataPreparser
{
    public static string GenerateInsightPrompt(List<string> rawJsonList)
    {
        var sb = new StringBuilder();
        int index = 1;

        foreach (var raw in rawJsonList)
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // PRIMA: Prova la struttura originale Tesla (response.data)
            if (root.TryGetProperty("response", out var response) && response.TryGetProperty("data", out var dataArray))
            {
                foreach (var item in dataArray.EnumerateArray())
                {
                    var type = item.GetProperty("type").GetString();
                    var content = item.GetProperty("content");

                    switch (type)
                    {
                        case "charging_history":
                            ProcessChargingHistory(sb, content, ref index);
                            break;
                        case "energy_endpoints":
                            ProcessEnergyEndpoints(sb, content, ref index);
                            break;
                        case "partner_public_key":
                            ProcessPartnerPublicKey(sb, content, ref index);
                            break;
                        case "user_profile":
                            ProcessUserProfile(sb, content, ref index);
                            break;
                        case "vehicle_commands":
                            ProcessVehicleCommands(sb, content, ref index);
                            break;
                        case "vehicle_endpoints":
                            ProcessVehicleEndpoints(sb, content, ref index);
                            break;
                        default:
                            sb.AppendLine($"[{index++}] Tipo dati non riconosciuto: {type}");
                            break;
                    }
                }
            }
            // NUOVO: Se non è struttura Tesla, prova struttura mock/semplice
            else
            {
                ProcessMockOrSimpleData(sb, root, ref index);
            }
        }

        return sb.ToString();
    }

    #region Original Tesla Data Processing Methods

    private static void ProcessChargingHistory(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind == JsonValueKind.Object)
        {
            var site = content.GetProperty("siteLocationName").GetString();
            var start = DateTime.Parse(content.GetProperty("chargeStartDateTime").GetString() ?? "");
            var stop = DateTime.Parse(content.GetProperty("chargeStopDateTime").GetString() ?? "");
            var mins = (stop - start).TotalMinutes;

            var vin = content.GetProperty("vin").GetString();
            var unlatch = content.GetProperty("unlatchDateTime").GetString();
            var country = content.GetProperty("countryCode").GetString();
            var billingType = content.GetProperty("billingType").GetString();
            var vehicleType = content.GetProperty("vehicleMakeType").GetString();
            var sessionId = content.GetProperty("sessionId").GetInt32();

            sb.AppendLine($"[{index++}] Sessione #{sessionId} – Ricarica a {site} ({country}), VIN {vin}");
            sb.AppendLine($"  - Inizio: {start:yyyy-MM-dd HH:mm}, Fine: {stop:yyyy-MM-dd HH:mm} ({mins:F0} minuti)");
            sb.AppendLine($"  - Tipo Veicolo: {vehicleType}, Billing: {billingType}");
            sb.AppendLine($"  - Rimozione cavo: {unlatch}");

            // Gestione fees più dettagliata
            if (content.TryGetProperty("fees", out var feesArray) && feesArray.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("  - COSTI:");
                foreach (var fee in feesArray.EnumerateArray())
                {
                    var feeType = fee.GetProperty("feeType").GetString();
                    var totalDue = fee.GetProperty("totalDue").GetDecimal();
                    var isPaid = fee.GetProperty("isPaid").GetBoolean();
                    var currency = fee.GetProperty("currencyCode").GetString();
                    var uom = fee.GetProperty("uom").GetString();
                    var status = fee.GetProperty("status").GetString();
                    var pricingType = fee.GetProperty("pricingType").GetString();

                    sb.AppendLine($"    • {feeType}: {totalDue} {currency} ({pricingType})");
                    sb.AppendLine($"      Unità: {uom}, Stato: {status} - {(isPaid ? "Pagato" : "Non pagato")}");

                    // Dettagli pricing tiers se presenti
                    if (fee.TryGetProperty("rateBase", out var rateBase) && rateBase.GetDecimal() > 0)
                    {
                        var usageBase = fee.GetProperty("usageBase").GetDecimal();
                        var totalBase = fee.GetProperty("totalBase").GetDecimal();
                        sb.AppendLine($"      Tariffa base: {rateBase.GetDecimal()} {currency}/{uom} × {usageBase} {uom} = {totalBase} {currency}");

                        // Tiers aggiuntivi se presenti
                        if (fee.TryGetProperty("usageTier2", out var tier2Usage) && tier2Usage.GetDecimal() > 0)
                        {
                            var rateTier2 = fee.GetProperty("rateTier2").GetDecimal();
                            var totalTier2 = fee.GetProperty("totalTier2").GetDecimal();
                            sb.AppendLine($"      Tier 2: {rateTier2} {currency}/{uom} × {tier2Usage.GetDecimal()} {uom} = {totalTier2} {currency}");
                        }
                    }

                    // Net due se diverso dal total due
                    if (fee.TryGetProperty("netDue", out var netDue) && netDue.GetDecimal() != totalDue)
                    {
                        sb.AppendLine($"      Netto da pagare: {netDue.GetDecimal()} {currency}");
                    }
                }
            }

            // Gestione invoices
            if (content.TryGetProperty("invoices", out var invoicesArray) && invoicesArray.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("  - FATTURE:");
                foreach (var invoice in invoicesArray.EnumerateArray())
                {
                    var fileName = invoice.GetProperty("fileName").GetString();
                    var invoiceType = invoice.GetProperty("invoiceType").GetString();
                    var contentId = invoice.TryGetProperty("contentId", out var cId) ? cId.GetString() : "N/A";
                    sb.AppendLine($"    • {fileName} (Tipo: {invoiceType}, ID: {contentId})");
                }
            }

            sb.AppendLine(); // spazio tra record
        }
    }

    private static void ProcessEnergyEndpoints(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine($"[{index++}] SISTEMA ENERGETICO - Stato Generale");

            // Live Status
            if (content.TryGetProperty("live_status", out var liveStatus) &&
                liveStatus.TryGetProperty("response", out var liveResponse))
            {
                var solarPower = liveResponse.GetProperty("solar_power").GetInt32();
                var energyLeft = liveResponse.GetProperty("energy_left").GetDecimal();
                var totalPackEnergy = liveResponse.GetProperty("total_pack_energy").GetInt32();
                var percentageCharged = liveResponse.GetProperty("percentage_charged").GetDecimal();
                var batteryPower = liveResponse.GetProperty("battery_power").GetInt32();
                var loadPower = liveResponse.GetProperty("load_power").GetInt32();
                var gridPower = liveResponse.GetProperty("grid_power").GetInt32();
                var gridStatus = liveResponse.GetProperty("grid_status").GetString();
                var islandStatus = liveResponse.GetProperty("island_status").GetString();
                var stormModeActive = liveResponse.GetProperty("storm_mode_active").GetBoolean();
                var backupCapable = liveResponse.GetProperty("backup_capable").GetBoolean();
                var timestamp = liveResponse.GetProperty("timestamp").GetString();

                sb.AppendLine("  - STATO LIVE:");
                sb.AppendLine($"    • Timestamp: {timestamp}");
                sb.AppendLine($"    • Produzione Solare: {solarPower} W");
                sb.AppendLine($"    • Batteria: {energyLeft:F2} Wh / {totalPackEnergy} Wh ({percentageCharged:F1}%)");
                sb.AppendLine($"    • Potenza Batteria: {batteryPower} W {(batteryPower > 0 ? "(scarica)" : "(ricarica)")}");
                sb.AppendLine($"    • Carico Casa: {loadPower} W");
                sb.AppendLine($"    • Rete Elettrica: {gridPower} W - Stato: {gridStatus} ({islandStatus})");
                sb.AppendLine($"    • Backup: {(backupCapable ? "Disponibile" : "Non disponibile")}");
                sb.AppendLine($"    • Storm Mode: {(stormModeActive ? "Attivo" : "Inattivo")}");
            }

            // Site Info
            if (content.TryGetProperty("site_info", out var siteInfo) &&
                siteInfo.TryGetProperty("response", out var siteResponse))
            {
                var siteName = siteResponse.GetProperty("site_name").GetString();
                var backupReserve = siteResponse.GetProperty("backup_reserve_percent").GetInt32();
                var realMode = siteResponse.GetProperty("default_real_mode").GetString();
                var installationDate = siteResponse.GetProperty("installation_date").GetString();
                var batteryCount = siteResponse.GetProperty("battery_count").GetInt32();
                var nameplatePower = siteResponse.GetProperty("nameplate_power").GetInt32();
                var nameplateEnergy = siteResponse.GetProperty("nameplate_energy").GetInt32();
                var version = siteResponse.GetProperty("version").GetString();

                sb.AppendLine("  - INFORMAZIONI IMPIANTO:");
                sb.AppendLine($"    • Nome: {siteName}");
                sb.AppendLine($"    • Installazione: {installationDate}");
                sb.AppendLine($"    • Modalità: {realMode}, Riserva Backup: {backupReserve}%");
                sb.AppendLine($"    • Batterie: {batteryCount} unità");
                sb.AppendLine($"    • Capacità Nominale: {nameplatePower} W / {nameplateEnergy} Wh");
                sb.AppendLine($"    • Versione Software: {version}");

                if (siteResponse.TryGetProperty("components", out var components))
                {
                    var hasSolar = components.GetProperty("solar").GetBoolean();
                    var hasBattery = components.GetProperty("battery").GetBoolean();
                    var hasGrid = components.GetProperty("grid").GetBoolean();
                    var solarType = components.TryGetProperty("solar_type", out var st) ? st.GetString() : "N/A";
                    var batteryType = components.TryGetProperty("battery_type", out var bt) ? bt.GetString() : "N/A";

                    sb.AppendLine($"    • Componenti: Solare({(hasSolar ? solarType : "No")}), Batteria({(hasBattery ? batteryType : "No")}), Rete({(hasGrid ? "Sì" : "No")})");
                }
            }

            // Energy History
            if (content.TryGetProperty("energy_history", out var energyHistory) &&
                energyHistory.TryGetProperty("response", out var energyResponse))
            {
                var period = energyResponse.GetProperty("period").GetString();
                sb.AppendLine($"  - STORICO ENERGETICO (Periodo: {period}):");

                if (energyResponse.TryGetProperty("time_series", out var timeSeries))
                {
                    foreach (var entry in timeSeries.EnumerateArray())
                    {
                        var timestamp = entry.GetProperty("timestamp").GetString();
                        var solarExported = entry.GetProperty("solar_energy_exported").GetInt32();
                        var gridImported = entry.GetProperty("grid_energy_imported").GetInt32();
                        var gridExported = entry.GetProperty("grid_energy_exported_from_solar").GetInt32();
                        var batteryExported = entry.GetProperty("battery_energy_exported").GetInt32();
                        var batteryImportedSolar = entry.GetProperty("battery_energy_imported_from_solar").GetInt32();
                        var consumerFromGrid = entry.GetProperty("consumer_energy_imported_from_grid").GetInt32();
                        var consumerFromSolar = entry.GetProperty("consumer_energy_imported_from_solar").GetInt32();
                        var consumerFromBattery = entry.GetProperty("consumer_energy_imported_from_battery").GetInt32();

                        sb.AppendLine($"    • {timestamp}:");
                        sb.AppendLine($"      Produzione Solare: {solarExported} Wh (esportato: {gridExported} Wh)");
                        sb.AppendLine($"      Rete: Importato {gridImported} Wh");
                        sb.AppendLine($"      Batteria: Esportato {batteryExported} Wh, Caricato da solare {batteryImportedSolar} Wh");
                        sb.AppendLine($"      Consumo Casa: Rete {consumerFromGrid} Wh + Solare {consumerFromSolar} Wh + Batteria {consumerFromBattery} Wh = {consumerFromGrid + consumerFromSolar + consumerFromBattery} Wh totali");
                    }
                }
            }

            // Charge History
            if (content.TryGetProperty("charge_history", out var chargeHistory) &&
                chargeHistory.TryGetProperty("response", out var chargeResponse) &&
                chargeResponse.TryGetProperty("charge_history", out var chargeArray))
            {
                sb.AppendLine("  - STORICO RICARICHE VEICOLI:");
                foreach (var charge in chargeArray.EnumerateArray())
                {
                    var startTime = DateTimeOffset.FromUnixTimeSeconds(charge.GetProperty("charge_start_time").GetProperty("seconds").GetInt64()).ToString("yyyy-MM-dd HH:mm");
                    var duration = charge.GetProperty("charge_duration").GetProperty("seconds").GetInt32();
                    var energyAdded = charge.GetProperty("energy_added_wh").GetInt32();
                    var durationHours = duration / 3600.0;

                    sb.AppendLine($"    • Inizio: {startTime}, Durata: {durationHours:F1}h, Energia: {energyAdded} Wh");
                }
            }

            // Backup History
            if (content.TryGetProperty("backup_history", out var backupHistory) &&
                backupHistory.TryGetProperty("response", out var backupResponse))
            {
                var totalEvents = backupResponse.GetProperty("total_events").GetInt32();
                sb.AppendLine($"  - EVENTI BACKUP ({totalEvents} eventi):");

                if (backupResponse.TryGetProperty("events", out var events))
                {
                    foreach (var evt in events.EnumerateArray())
                    {
                        var timestamp = evt.GetProperty("timestamp").GetString();
                        var duration = evt.GetProperty("duration").GetInt32();
                        var durationHours = duration / 3600.0;

                        sb.AppendLine($"    • {timestamp}: Durata {durationHours:F1}h ({duration}s)");
                    }
                }
            }

            // Products
            if (content.TryGetProperty("products", out var products) &&
                products.TryGetProperty("response", out var productArray))
            {
                var count = products.GetProperty("count").GetInt32();
                sb.AppendLine($"  - PRODOTTI COLLEGATI ({count} dispositivi):");

                foreach (var product in productArray.EnumerateArray())
                {
                    if (product.TryGetProperty("device_type", out var deviceType))
                    {
                        var deviceTypeStr = deviceType.GetString();
                        if (deviceTypeStr == "vehicle")
                        {
                            var vin = product.TryGetProperty("vin", out var v) ? v.GetString() : "N/A";
                            var displayName = product.TryGetProperty("display_name", out var dn) ? dn.GetString() : "N/A";
                            var state = product.TryGetProperty("state", out var s) ? s.GetString() : "N/A";
                            var accessType = product.TryGetProperty("access_type", out var at) ? at.GetString() : "N/A";

                            sb.AppendLine($"    • Veicolo: {displayName} (VIN: {vin}) - Stato: {state}, Accesso: {accessType}");
                        }
                        else if (deviceTypeStr == "energy")
                        {
                            var siteName = product.TryGetProperty("site_name", out var sn) ? sn.GetString() : "N/A";
                            var resourceType = product.TryGetProperty("resource_type", out var rt) ? rt.GetString() : "N/A";
                            var energyLeft = product.TryGetProperty("energy_left", out var el) ? el.GetInt32() : 0;
                            var totalEnergy = product.TryGetProperty("total_pack_energy", out var te) ? te.GetInt32() : 0;
                            var percentage = product.TryGetProperty("percentage_charged", out var pc) ? pc.GetInt32() : 0;

                            sb.AppendLine($"    • Sistema Energetico: {siteName} ({resourceType}) - {energyLeft}/{totalEnergy} Wh ({percentage}%)");
                        }
                    }
                }
            }

            // Status Updates (backup, operation, storm_mode, etc.)
            var statusUpdates = new List<string>();

            if (content.TryGetProperty("backup", out var backup) &&
                backup.TryGetProperty("response", out var backupResp))
            {
                var code = backupResp.GetProperty("code").GetInt32();
                var message = backupResp.GetProperty("message").GetString();
                statusUpdates.Add($"Backup: {message} (Code: {code})");
            }

            if (content.TryGetProperty("operation", out var operation) &&
                operation.TryGetProperty("response", out var operationResp))
            {
                var code = operationResp.GetProperty("code").GetInt32();
                var message = operationResp.GetProperty("message").GetString();
                statusUpdates.Add($"Operazioni: {message} (Code: {code})");
            }

            if (content.TryGetProperty("storm_mode", out var stormMode) &&
                stormMode.TryGetProperty("response", out var stormResp))
            {
                var code = stormResp.GetProperty("code").GetInt32();
                var message = stormResp.GetProperty("message").GetString();
                statusUpdates.Add($"Storm Mode: {message} (Code: {code})");
            }

            if (statusUpdates.Any())
            {
                sb.AppendLine("  - AGGIORNAMENTI STATO:");
                foreach (var update in statusUpdates)
                {
                    sb.AppendLine($"    • {update}");
                }
            }

            sb.AppendLine(); // spazio tra record
        }
    }

    private static void ProcessPartnerPublicKey(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine($"[{index++}] CONFIGURAZIONE PARTNER API");

            // Public Key
            if (content.TryGetProperty("public_key", out var publicKey))
            {
                var key = publicKey.GetString();
                var keyPreview = key?.Length > 20 ? $"{key[..20]}...{key[^10..]}" : key;
                sb.AppendLine($"  - CHIAVE PUBBLICA: {keyPreview} (lunghezza: {key?.Length} caratteri)");
            }

            // Fleet Telemetry Error VINs
            if (content.TryGetProperty("fleet_telemetry_error_vins", out var errorVins) &&
                errorVins.ValueKind == JsonValueKind.Array)
            {
                var vinCount = errorVins.GetArrayLength();
                sb.AppendLine($"  - VIN CON ERRORI TELEMETRIA ({vinCount} veicoli):");

                foreach (var vin in errorVins.EnumerateArray())
                {
                    sb.AppendLine($"    • {vin.GetString()}");
                }
            }

            // Fleet Telemetry Errors
            if (content.TryGetProperty("fleet_telemetry_errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array)
            {
                var errorCount = errors.GetArrayLength();
                sb.AppendLine($"  - DETTAGLI ERRORI TELEMETRIA ({errorCount} errori):");

                foreach (var error in errors.EnumerateArray())
                {
                    var clientName = error.GetProperty("name").GetString();
                    var errorMessage = error.GetProperty("error").GetString();
                    var vin = error.GetProperty("vin").GetString();

                    sb.AppendLine($"    • Client: {clientName}");
                    sb.AppendLine($"      VIN: {vin}");
                    sb.AppendLine($"      Errore: {errorMessage}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine(); // spazio tra record
        }
    }

    private static void ProcessUserProfile(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine($"[{index++}] PROFILO UTENTE E CONFIGURAZIONE");

            // User Information
            if (content.TryGetProperty("me", out var me) &&
                me.TryGetProperty("response", out var meResponse))
            {
                var email = meResponse.GetProperty("email").GetString();
                var fullName = meResponse.GetProperty("full_name").GetString();
                var profileImageUrl = meResponse.GetProperty("profile_image_url").GetString();
                var vaultUuid = meResponse.GetProperty("vault_uuid").GetString();

                sb.AppendLine("  - INFORMAZIONI UTENTE:");
                sb.AppendLine($"    • Nome: {fullName}");
                sb.AppendLine($"    • Email: {email}");
                sb.AppendLine($"    • Immagine Profilo: {profileImageUrl}");
                sb.AppendLine($"    • Vault UUID: {vaultUuid}");
            }

            // Region Information
            if (content.TryGetProperty("region", out var region) &&
                region.TryGetProperty("response", out var regionResponse))
            {
                var regionCode = regionResponse.GetProperty("region").GetString();
                var fleetApiUrl = regionResponse.GetProperty("fleet_api_base_url").GetString();

                sb.AppendLine("  - CONFIGURAZIONE REGIONALE:");
                sb.AppendLine($"    • Regione: {regionCode?.ToUpper()}");
                sb.AppendLine($"    • Fleet API Base URL: {fleetApiUrl}");
            }

            // Feature Configuration
            if (content.TryGetProperty("feature_config", out var featureConfig) &&
                featureConfig.TryGetProperty("response", out var featureResponse))
            {
                sb.AppendLine("  - CONFIGURAZIONE FUNZIONALITÀ:");

                if (featureResponse.TryGetProperty("signaling", out var signaling))
                {
                    var enabled = signaling.GetProperty("enabled").GetBoolean();
                    var subscribeConnectivity = signaling.GetProperty("subscribe_connectivity").GetBoolean();
                    var useAuthToken = signaling.GetProperty("use_auth_token").GetBoolean();

                    sb.AppendLine($"    • Signaling: {(enabled ? "Abilitato" : "Disabilitato")}");
                    sb.AppendLine($"    • Subscribe Connectivity: {(subscribeConnectivity ? "Sì" : "No")}");
                    sb.AppendLine($"    • Use Auth Token: {(useAuthToken ? "Sì" : "No")}");
                }
            }

            // Orders
            if (content.TryGetProperty("orders", out var orders))
            {
                var count = orders.GetProperty("count").GetInt32();
                sb.AppendLine($"  - ORDINI ({count} ordini):");

                if (orders.TryGetProperty("response", out var ordersArray) &&
                    ordersArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var order in ordersArray.EnumerateArray())
                    {
                        var vehicleMapId = order.GetProperty("vehicleMapId").GetInt32();
                        var referenceNumber = order.GetProperty("referenceNumber").GetString();
                        var vin = order.GetProperty("vin").GetString();
                        var orderStatus = order.GetProperty("orderStatus").GetString();
                        var orderSubstatus = order.GetProperty("orderSubstatus").GetString();
                        var modelCode = order.GetProperty("modelCode").GetString();
                        var countryCode = order.GetProperty("countryCode").GetString();
                        var locale = order.GetProperty("locale").GetString();
                        var mktOptions = order.GetProperty("mktOptions").GetString();
                        var isB2b = order.GetProperty("isB2b").GetBoolean();

                        sb.AppendLine($"    • Ordine #{referenceNumber} (ID: {vehicleMapId})");
                        sb.AppendLine($"      VIN: {vin}");
                        sb.AppendLine($"      Modello: {modelCode?.ToUpper()}, Paese: {countryCode}, Locale: {locale}");
                        sb.AppendLine($"      Stato: {orderStatus} ({orderSubstatus})");
                        sb.AppendLine($"      Tipo: {(isB2b ? "Business (B2B)" : "Privato (B2C)")}");

                        // Parse market options if present
                        if (!string.IsNullOrEmpty(mktOptions))
                        {
                            var options = mktOptions.Split(',');
                            sb.AppendLine($"      Opzioni: {string.Join(", ", options)} ({options.Length} opzioni)");
                        }
                        sb.AppendLine();
                    }
                }
            }

            sb.AppendLine(); // spazio tra record
        }
    }

    private static void ProcessVehicleCommands(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine($"[{index++}] COMANDI VEICOLO ESEGUITI");

            var commandsByCategory = new Dictionary<string, List<JsonElement>>();

            // Raggruppa i comandi per categoria
            foreach (var command in content.EnumerateArray())
            {
                var commandName = command.GetProperty("command").GetString() ?? "[comando non specificato]";
                var category = GetCommandCategory(commandName);

                if (!commandsByCategory.ContainsKey(category))
                    commandsByCategory[category] = [];

                commandsByCategory[category].Add(command);
            }

            // Mostra i comandi raggruppati per categoria
            foreach (var category in commandsByCategory.Keys.OrderBy(k => k))
            {
                sb.AppendLine($"  - {category.ToUpper()}:");

                foreach (var command in commandsByCategory[category])
                {
                    var commandName = command.GetProperty("command").GetString() ?? "[comando sconosciuto]";
                    var timestamp = command.GetProperty("timestamp").GetString();
                    var commandResponse = command.GetProperty("response");

                    var result = commandResponse.GetProperty("result").GetBoolean();
                    var reason = commandResponse.TryGetProperty("reason", out var r) ? r.GetString() : "";

                    var parsedTimeOk = DateTime.TryParse(timestamp, out var parsedTime);
                    var time = parsedTimeOk ? parsedTime.ToString("HH:mm:ss") : "[orario sconosciuto]";
                    var status = result ? "✓ Successo" : $"✗ Errore: {reason}";

                    sb.AppendLine($"    • {time} - {GetCommandDisplayName(commandName)}: {status}");

                    // Mostra parametri se presenti
                    if (command.TryGetProperty("parameters", out var parameters) &&
                        parameters.ValueKind == JsonValueKind.Object)
                    {
                        var paramDetails = FormatCommandParameters(commandName, parameters);
                        if (!string.IsNullOrEmpty(paramDetails))
                        {
                            sb.AppendLine($"      {paramDetails}");
                        }
                    }

                    // Mostra informazioni aggiuntive dalla response se presenti
                    if (commandResponse.TryGetProperty("queued", out var queued))
                    {
                        sb.AppendLine($"      In coda: {(queued.GetBoolean() ? "Sì" : "No")}");
                    }
                }
                sb.AppendLine();
            }

            // Statistiche riassuntive
            var totalCommands = content.GetArrayLength();
            var successfulCommands = content.EnumerateArray().Count(c => c.GetProperty("response").GetProperty("result").GetBoolean());
            var failedCommands = totalCommands - successfulCommands;

            sb.AppendLine($"  - RIEPILOGO: {totalCommands} comandi totali - {successfulCommands} riusciti, {failedCommands} falliti");
            sb.AppendLine();
        }
    }

    private static void ProcessVehicleEndpoints(StringBuilder sb, JsonElement content, ref int index)
    {
        if (content.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine($"[{index++}] ENDPOINTS VEICOLO - Stato Completo");

            // Vehicle Data - Core Information
            if (content.TryGetProperty("vehicle_data", out var vehicleData) &&
                vehicleData.TryGetProperty("response", out var vdResponse))
            {
                var vin = vdResponse.GetProperty("vin").GetString();
                var state = vdResponse.GetProperty("state").GetString();
                var accessType = vdResponse.GetProperty("access_type").GetString();
                var inService = vdResponse.GetProperty("in_service").GetBoolean();
                var apiVersion = vdResponse.TryGetProperty("api_version", out var av) ? av.GetInt32().ToString() : "N/A";

                sb.AppendLine("  - INFORMAZIONI VEICOLO:");
                sb.AppendLine($"    • VIN: {vin}");
                sb.AppendLine($"    • Stato: {state}, Accesso: {accessType}");
                sb.AppendLine($"    • In Servizio: {(inService ? "Sì" : "No")}, API Version: {apiVersion}");

                // Charge State
                if (vdResponse.TryGetProperty("charge_state", out var chargeState))
                {
                    var batteryLevel = chargeState.GetProperty("battery_level").GetInt32();
                    var batteryRange = chargeState.GetProperty("battery_range").GetDecimal();
                    var chargingState = chargeState.GetProperty("charging_state").GetString();
                    var chargeLimit = chargeState.GetProperty("charge_limit_soc").GetInt32();
                    var chargeRate = chargeState.GetProperty("charge_rate").GetDecimal();
                    var minutesToFull = chargeState.GetProperty("minutes_to_full_charge").GetInt32();

                    sb.AppendLine("    • STATO RICARICA:");
                    sb.AppendLine($"      Batteria: {batteryLevel}% ({batteryRange:F1} km), Limite: {chargeLimit}%");
                    sb.AppendLine($"      Stato: {chargingState}, Velocità: {chargeRate} km/h");
                    if (minutesToFull > 0) sb.AppendLine($"      Tempo rimasto: {minutesToFull} minuti");
                }

                // Climate State
                if (vdResponse.TryGetProperty("climate_state", out var climateState))
                {
                    var insideTemp = climateState.GetProperty("inside_temp").GetDecimal();
                    var outsideTemp = climateState.GetProperty("outside_temp").GetDecimal();
                    var driverTemp = climateState.GetProperty("driver_temp_setting").GetDecimal();
                    var passengerTemp = climateState.GetProperty("passenger_temp_setting").GetDecimal();
                    var isClimateOn = climateState.GetProperty("is_climate_on").GetBoolean();
                    var cabinOverheat = climateState.GetProperty("cabin_overheat_protection").GetString();

                    sb.AppendLine("    • CLIMA:");
                    sb.AppendLine($"      Temperature: Interna {insideTemp:F1}°C, Esterna {outsideTemp:F1}°C");
                    sb.AppendLine($"      Impostazioni: Guidatore {driverTemp:F1}°C, Passeggero {passengerTemp:F1}°C");
                    sb.AppendLine($"      Sistema: {(isClimateOn ? "Acceso" : "Spento")}, Protezione: {cabinOverheat}");
                }

                // Drive State
                if (vdResponse.TryGetProperty("drive_state", out var driveState))
                {
                    var latitude = driveState.TryGetProperty("latitude", out var lat) ? lat.GetDecimal() : 0;
                    var longitude = driveState.TryGetProperty("longitude", out var lon) ? lon.GetDecimal() : 0;
                    var heading = driveState.TryGetProperty("heading", out var h) ? h.GetInt32() : 0;
                    var speed = driveState.TryGetProperty("speed", out var s) && s.ValueKind != JsonValueKind.Null ? s.GetInt32().ToString() : "Fermo";

                    sb.AppendLine("    • POSIZIONE E GUIDA:");
                    sb.AppendLine($"      Coordinate: {latitude:F6}, {longitude:F6}");
                    sb.AppendLine($"      Direzione: {heading}°, Velocità: {speed} km/h");
                }

                // Vehicle State
                if (vdResponse.TryGetProperty("vehicle_state", out var vehicleState))
                {
                    var locked = vehicleState.GetProperty("locked").GetBoolean();
                    var sentryMode = vehicleState.GetProperty("sentry_mode").GetBoolean();
                    var valetMode = vehicleState.GetProperty("valet_mode").GetBoolean();
                    var odometer = vehicleState.GetProperty("odometer").GetDecimal();
                    var vehicleName = vehicleState.TryGetProperty("vehicle_name", out var vn) ? vn.GetString() : "N/A";

                    sb.AppendLine("    • STATO VEICOLO:");
                    sb.AppendLine($"      Nome: {vehicleName}, Chilometraggio: {odometer:F1} km");
                    sb.AppendLine($"      Bloccato: {(locked ? "Sì" : "No")}, Sentry: {(sentryMode ? "Attivo" : "Inattivo")}, Valet: {(valetMode ? "Attivo" : "Inattivo")}");

                    // TPMS
                    var tpmsFL = vehicleState.GetProperty("tpms_pressure_fl").GetDecimal();
                    var tpmsFR = vehicleState.GetProperty("tpms_pressure_fr").GetDecimal();
                    var tpmsRL = vehicleState.GetProperty("tpms_pressure_rl").GetDecimal();
                    var tpmsRR = vehicleState.GetProperty("tpms_pressure_rr").GetDecimal();
                    sb.AppendLine($"      Pressioni Pneumatici: AS {tpmsFL:F1} bar, AD {tpmsFR:F1} bar, PS {tpmsRL:F1} bar, PD {tpmsRR:F1} bar");
                }
            }

            // Vehicle List
            if (content.TryGetProperty("list", out var list) &&
                list.TryGetProperty("response", out var listResponse))
            {
                var count = list.GetProperty("count").GetInt32();
                sb.AppendLine($"  - VEICOLI ASSOCIATI ({count} veicoli):");

                foreach (var vehicle in listResponse.EnumerateArray())
                {
                    var vin = vehicle.GetProperty("vin").GetString();
                    var displayName = vehicle.GetProperty("display_name").GetString();
                    var state = vehicle.GetProperty("state").GetString();
                    var accessType = vehicle.GetProperty("access_type").GetString();

                    sb.AppendLine($"    • {displayName} (VIN: {vin}) - {state}, {accessType}");
                }
            }

            // Drivers
            if (content.TryGetProperty("drivers", out var drivers) &&
                drivers.TryGetProperty("response", out var driversResponse))
            {
                var driverCount = drivers.GetProperty("count").GetInt32();
                sb.AppendLine($"  - GUIDATORI AUTORIZZATI ({driverCount} guidatori):");

                foreach (var driver in driversResponse.EnumerateArray())
                {
                    var firstName = driver.GetProperty("driver_first_name").GetString();
                    var lastName = driver.GetProperty("driver_last_name").GetString();
                    var userId = driver.GetProperty("user_id").GetInt32();

                    sb.AppendLine($"    • {firstName} {lastName} (ID: {userId})");
                }
            }

            // Eligible Subscriptions
            if (content.TryGetProperty("eligible_subscriptions", out var eligibleSubs) &&
                eligibleSubs.TryGetProperty("response", out var eligibleResponse))
            {
                sb.AppendLine("  - ABBONAMENTI DISPONIBILI:");

                if (eligibleResponse.TryGetProperty("eligible", out var eligible))
                {
                    foreach (var subscription in eligible.EnumerateArray())
                    {
                        var product = subscription.GetProperty("product").GetString();
                        var optionCode = subscription.GetProperty("optionCode").GetString();

                        sb.AppendLine($"    • {product} ({optionCode})");

                        if (subscription.TryGetProperty("billingOptions", out var billingOptions))
                        {
                            foreach (var billing in billingOptions.EnumerateArray())
                            {
                                var period = billing.GetProperty("billingPeriod").GetString();
                                var total = billing.GetProperty("total").GetDecimal();
                                var currency = billing.GetProperty("currencyCode").GetString();

                                sb.AppendLine($"      {period}: {total} {currency}");
                            }
                        }
                    }
                }
            }

            // Eligible Upgrades
            if (content.TryGetProperty("eligible_upgrades", out var eligibleUpgrades) &&
                eligibleUpgrades.TryGetProperty("response", out var upgradesResponse))
            {
                sb.AppendLine("  - UPGRADE DISPONIBILI:");

                if (upgradesResponse.TryGetProperty("eligible", out var upgrades))
                {
                    foreach (var upgrade in upgrades.EnumerateArray())
                    {
                        var optionCode = upgrade.GetProperty("optionCode").GetString();
                        var optionGroup = upgrade.GetProperty("optionGroup").GetString();

                        sb.AppendLine($"    • {optionGroup}: {optionCode}");

                        if (upgrade.TryGetProperty("pricing", out var pricing))
                        {
                            foreach (var price in pricing.EnumerateArray())
                            {
                                var total = price.GetProperty("total").GetDecimal();
                                var currency = price.GetProperty("currencyCode").GetString();
                                var isPrimary = price.GetProperty("isPrimary").GetBoolean();

                                sb.AppendLine($"      Prezzo{(isPrimary ? " (Primario)" : "")}: {total} {currency}");
                            }
                        }
                    }
                }
            }

            // Fleet Status
            if (content.TryGetProperty("fleet_status", out var fleetStatus) &&
                fleetStatus.TryGetProperty("response", out var fleetResponse))
            {
                var keyPairedCount = fleetResponse.GetProperty("key_paired_vins").GetArrayLength();
                var unpairedCount = fleetResponse.GetProperty("unpaired_vins").GetArrayLength();

                sb.AppendLine($"  - STATO FLOTTA: {keyPairedCount} veicoli collegati, {unpairedCount} non collegati");

                if (fleetResponse.TryGetProperty("vehicle_info", out var vehicleInfo))
                {
                    foreach (var prop in vehicleInfo.EnumerateObject())
                    {
                        var vin = prop.Name;
                        var info = prop.Value;
                        var firmware = info.GetProperty("firmware_version").GetString();
                        var telemetryVersion = info.GetProperty("fleet_telemetry_version").GetString();
                        var totalKeys = info.GetProperty("total_number_of_keys").GetInt32();

                        sb.AppendLine($"    • {vin}: FW {firmware}, Telemetry {telemetryVersion}, {totalKeys} chiavi");
                    }
                }
            }

            // Nearby Charging Sites
            if (content.TryGetProperty("nearby_charging_sites", out var chargingSites) &&
                chargingSites.TryGetProperty("response", out var sitesResponse))
            {
                sb.AppendLine("  - STAZIONI RICARICA VICINE:");

                if (sitesResponse.TryGetProperty("superchargers", out var superchargers))
                {
                    sb.AppendLine("    Supercharger:");
                    foreach (var sc in superchargers.EnumerateArray())
                    {
                        var name = sc.GetProperty("name").GetString();
                        var distance = sc.GetProperty("distance_miles").GetDecimal();
                        var available = sc.GetProperty("available_stalls").GetInt32();
                        var total = sc.GetProperty("total_stalls").GetInt32();

                        sb.AppendLine($"      • {name}: {available}/{total} stalli, {distance:F1} miglia");
                    }
                }

                if (sitesResponse.TryGetProperty("destination_charging", out var destinations))
                {
                    sb.AppendLine("    Destination Charging:");
                    foreach (var dest in destinations.EnumerateArray())
                    {
                        var name = dest.GetProperty("name").GetString();
                        var distance = dest.GetProperty("distance_miles").GetDecimal();
                        var amenities = dest.TryGetProperty("amenities", out var a) ? a.GetString() : "";

                        sb.AppendLine($"      • {name}: {distance:F1} miglia ({amenities})");
                    }
                }
            }

            // Vehicle Options
            if (content.TryGetProperty("options", out var options) &&
                options.TryGetProperty("response", out var optionsResponse))
            {
                sb.AppendLine("  - OPZIONI VEICOLO:");

                if (optionsResponse.TryGetProperty("codes", out var codes))
                {
                    foreach (var option in codes.EnumerateArray())
                    {
                        var code = option.GetProperty("code").GetString();
                        var displayName = option.GetProperty("displayName").GetString();
                        var isActive = option.GetProperty("isActive").GetBoolean();

                        sb.AppendLine($"    • {displayName} ({code}){(isActive ? " ✓" : "")}");
                    }
                }
            }

            // Warranty Details
            if (content.TryGetProperty("warranty_details", out var warranty) &&
                warranty.TryGetProperty("response", out var warrantyResponse))
            {
                sb.AppendLine("  - GARANZIE ATTIVE:");

                if (warrantyResponse.TryGetProperty("activeWarranty", out var activeWarranties))
                {
                    foreach (var w in activeWarranties.EnumerateArray())
                    {
                        var displayName = w.GetProperty("warrantyDisplayName").GetString() ?? "[Garanzia sconosciuta]";
                        var expirationDate = w.GetProperty("expirationDate").GetString();
                        var expirationOdometer = w.GetProperty("expirationOdometer").GetInt32();
                        var odometerUnit = w.GetProperty("odometerUnit").GetString() ?? "";

                        var parsedTimeOk = DateTime.TryParse(expirationDate, out var expDt);
                        var expDate = parsedTimeOk ? expDt.ToString("yyyy-MM-dd") : "[data sconosciuta]";
                        sb.AppendLine($"    • {displayName}: fino al {expDate} o {expirationOdometer:N0} {odometerUnit}");
                    }
                }
            }

            // Share Invites
            if (content.TryGetProperty("share_invites", out var shareInvites) &&
                shareInvites.TryGetProperty("response", out var invitesResponse))
            {
                var inviteCount = shareInvites.GetProperty("count").GetInt32();
                sb.AppendLine($"  - INVITI CONDIVISIONE ({inviteCount} inviti):");

                foreach (var invite in invitesResponse.EnumerateArray())
                {
                    var state = invite.GetProperty("state").GetString() ?? "[stato sconosciuto]";
                    var shareType = invite.GetProperty("share_type").GetString() ?? "[tipo sconosciuto]";
                    var expiresAt = invite.GetProperty("expires_at").GetString();
                    var vin = invite.GetProperty("vin").GetString() ?? "[VIN mancante]";

                    var parsedTimeOk = DateTime.TryParse(expiresAt, out var parsedDate);
                    var expDate = parsedTimeOk ? parsedDate.ToString("yyyy-MM-dd") : "[data sconosciuta]";
                    sb.AppendLine($"    • VIN {vin}: {shareType}, Stato: {state}, Scade: {expDate}");
                }
            }

            // Recent Alerts
            if (content.TryGetProperty("recent_alerts", out var alerts) &&
                alerts.TryGetProperty("response", out var alertsResponse))
            {
                sb.AppendLine("  - AVVISI RECENTI:");

                if (alertsResponse.TryGetProperty("recent_alerts", out var recentAlerts))
                {
                    foreach (var alert in recentAlerts.EnumerateArray())
                    {
                        var name = alert.GetProperty("name").GetString();
                        var time = alert.GetProperty("time").GetString();
                        var userText = alert.TryGetProperty("user_text", out var ut) ? ut.GetString() : "";

                        var parsedTimeOk = DateTime.TryParse(time, out var dt);
                        var alertTime = parsedTimeOk ? dt.ToString("yyyy-MM-dd HH:mm") : "[orario sconosciuto]";
                        sb.AppendLine($"    • {alertTime} - {name}: {userText}");
                    }
                }
            }

            // Service Data
            if (content.TryGetProperty("service_data", out var serviceData) &&
                serviceData.TryGetProperty("response", out var serviceResponse))
            {
                var serviceStatus = serviceResponse.GetProperty("service_status").GetString();
                var serviceEtc = serviceResponse.TryGetProperty("service_etc", out var etc) ? etc.GetString() : "";
                var visitNumber = serviceResponse.TryGetProperty("service_visit_number", out var vn) ? vn.GetString() : "";

                sb.AppendLine("  - STATO ASSISTENZA:");
                sb.AppendLine($"    • Stato: {serviceStatus}");
                if (!string.IsNullOrEmpty(serviceEtc)) sb.AppendLine($"    • Completamento previsto: {serviceEtc}");
                if (!string.IsNullOrEmpty(visitNumber)) sb.AppendLine($"    • Numero visita: {visitNumber}");
            }

            sb.AppendLine(); // spazio tra record
        }
    }

    #endregion

    #region New Mock/Simple Data Processing

    /// <summary>
    /// NUOVO: Gestisce dati mock o semplici (non struttura Tesla originale)
    /// </summary>
    private static void ProcessMockOrSimpleData(StringBuilder sb, JsonElement root, ref int index)
    {
        sb.AppendLine($"[{index++}] DATI VEICOLO TESLA - Campione {DateTime.UtcNow:HH:mm:ss}");

        // Se è un array diretto, processa ogni elemento
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                ProcessSingleMockData(sb, item);
            }
        }
        // Se è un oggetto diretto, processalo
        else if (root.ValueKind == JsonValueKind.Object)
        {
            ProcessSingleMockData(sb, root);
        }
        else
        {
            sb.AppendLine("  - Formato dati non riconosciuto");
        }

        sb.AppendLine(); // spazio tra record
    }

    private static void ProcessSingleMockData(StringBuilder sb, JsonElement data)
    {
        // Cerca timestamp se presente
        if (data.TryGetProperty("timestamp", out var timestamp))
        {
            sb.AppendLine($"  - Timestamp: {timestamp.GetString()}");
        }

        // Dati veicolo simulati
        if (data.TryGetProperty("vehicle_state", out var vehicleState))
        {
            sb.AppendLine("  - STATO VEICOLO:");
            ProcessMockVehicleState(sb, vehicleState);
        }

        // Dati batteria simulati
        if (data.TryGetProperty("charge_state", out var chargeState))
        {
            sb.AppendLine("  - STATO RICARICA:");
            ProcessMockChargeState(sb, chargeState);
        }

        // Dati climatizzazione simulati
        if (data.TryGetProperty("climate_state", out var climateState))
        {
            sb.AppendLine("  - CLIMATIZZAZIONE:");
            ProcessMockClimateState(sb, climateState);
        }

        // Dati posizione simulati
        if (data.TryGetProperty("drive_state", out var driveState))
        {
            sb.AppendLine("  - POSIZIONE E GUIDA:");
            ProcessMockDriveState(sb, driveState);
        }

        // Dati GUI settings se presenti
        if (data.TryGetProperty("gui_settings", out var guiSettings))
        {
            sb.AppendLine("  - IMPOSTAZIONI INTERFACCIA:");
            ProcessMockGuiSettings(sb, guiSettings);
        }

        // Se non trova nessuna sezione specifica, mostra tutti i campi
        if (!data.TryGetProperty("vehicle_state", out _) &&
            !data.TryGetProperty("charge_state", out _) &&
            !data.TryGetProperty("climate_state", out _) &&
            !data.TryGetProperty("drive_state", out _))
        {
            sb.AppendLine("  - DATI GENERICI:");
            foreach (var prop in data.EnumerateObject())
            {
                var value = FormatJsonValue(prop.Value);
                sb.AppendLine($"    • {prop.Name}: {value}");
            }
        }
    }

    private static void ProcessMockVehicleState(StringBuilder sb, JsonElement vehicleState)
    {
        sb.AppendLine($"    • Bloccato: {GetSafeStringValue(vehicleState, "locked", GetSafeBooleanValue(vehicleState, "locked") ? "Sì" : "No")}");
        sb.AppendLine($"    • Sentry Mode: {GetSafeStringValue(vehicleState, "sentry_mode", GetSafeBooleanValue(vehicleState, "sentry_mode") ? "Attivo" : "Inattivo")}");
        sb.AppendLine($"    • Modalità Valet: {GetSafeStringValue(vehicleState, "valet_mode", GetSafeBooleanValue(vehicleState, "valet_mode") ? "Attiva" : "Inattiva")}");

        var odometer = GetSafeDecimalValue(vehicleState, "odometer");
        if (odometer > 0)
        {
            sb.AppendLine($"    • Chilometraggio: {odometer:F1} km");
        }

        var vehicleName = GetSafeStringValue(vehicleState, "vehicle_name");
        if (!string.IsNullOrEmpty(vehicleName))
        {
            sb.AppendLine($"    • Nome Veicolo: {vehicleName}");
        }

        // Pressioni pneumatici se presenti
        if (vehicleState.TryGetProperty("tpms_pressure_fl", out _))
        {
            sb.AppendLine($"    • Pressioni Pneumatici:");
            sb.AppendLine($"      Anteriore SX: {GetSafeDecimalValue(vehicleState, "tpms_pressure_fl"):F1} bar");
            sb.AppendLine($"      Anteriore DX: {GetSafeDecimalValue(vehicleState, "tpms_pressure_fr"):F1} bar");
            sb.AppendLine($"      Posteriore SX: {GetSafeDecimalValue(vehicleState, "tpms_pressure_rl"):F1} bar");
            sb.AppendLine($"      Posteriore DX: {GetSafeDecimalValue(vehicleState, "tpms_pressure_rr"):F1} bar");
        }

        // Software version se presente
        var softwareVersion = GetSafeStringValue(vehicleState, "car_version");
        if (!string.IsNullOrEmpty(softwareVersion))
        {
            sb.AppendLine($"    • Versione Software: {softwareVersion}");
        }
    }

    private static void ProcessMockChargeState(StringBuilder sb, JsonElement chargeState)
    {
        var batteryLevel = GetSafeIntValue(chargeState, "battery_level");
        var batteryRange = GetSafeDecimalValue(chargeState, "battery_range");
        var chargingState = GetSafeStringValue(chargeState, "charging_state", "Sconosciuto");
        var chargeLimit = GetSafeIntValue(chargeState, "charge_limit_soc");

        sb.AppendLine($"    • Livello Batteria: {batteryLevel}%");
        sb.AppendLine($"    • Autonomia: {batteryRange:F1} km");
        sb.AppendLine($"    • Stato Ricarica: {chargingState}");

        if (chargeLimit > 0)
        {
            sb.AppendLine($"    • Limite Ricarica: {chargeLimit}%");
        }

        var chargeRate = GetSafeDecimalValue(chargeState, "charge_rate");
        if (chargeRate > 0)
        {
            sb.AppendLine($"    • Velocità Ricarica: {chargeRate} km/h");
        }

        var minutesToFull = GetSafeIntValue(chargeState, "minutes_to_full_charge");
        if (minutesToFull > 0)
        {
            sb.AppendLine($"    • Tempo per Ricarica Completa: {minutesToFull} minuti");
        }

        var chargePortOpen = GetSafeBooleanValue(chargeState, "charge_port_door_open");
        sb.AppendLine($"    • Sportello Ricarica: {(chargePortOpen ? "Aperto" : "Chiuso")}");

        var fastChargerPresent = GetSafeBooleanValue(chargeState, "fast_charger_present");
        if (fastChargerPresent)
        {
            sb.AppendLine($"    • Fast Charger: Presente");
        }

        // Energia caricata se presente
        var chargeEnergyAdded = GetSafeDecimalValue(chargeState, "charge_energy_added");
        if (chargeEnergyAdded > 0)
        {
            sb.AppendLine($"    • Energia Aggiunta: {chargeEnergyAdded:F1} kWh");
        }
    }

    private static void ProcessMockClimateState(StringBuilder sb, JsonElement climateState)
    {
        var insideTemp = GetSafeDecimalValue(climateState, "inside_temp");
        var outsideTemp = GetSafeDecimalValue(climateState, "outside_temp");
        var driverTemp = GetSafeDecimalValue(climateState, "driver_temp_setting");
        var passengerTemp = GetSafeDecimalValue(climateState, "passenger_temp_setting");
        var isClimateOn = GetSafeBooleanValue(climateState, "is_climate_on");

        sb.AppendLine($"    • Temperatura Interna: {insideTemp:F1}°C");
        sb.AppendLine($"    • Temperatura Esterna: {outsideTemp:F1}°C");
        sb.AppendLine($"    • Sistema Climatizzazione: {(isClimateOn ? "Acceso" : "Spento")}");

        if (driverTemp > 0)
        {
            sb.AppendLine($"    • Impostazione Guidatore: {driverTemp:F1}°C");
        }

        if (passengerTemp > 0)
        {
            sb.AppendLine($"    • Impostazione Passeggero: {passengerTemp:F1}°C");
        }

        var cabinOverheat = GetSafeStringValue(climateState, "cabin_overheat_protection");
        if (!string.IsNullOrEmpty(cabinOverheat))
        {
            sb.AppendLine($"    • Protezione Surriscaldamento: {cabinOverheat}");
        }

        var smartPreconditioning = GetSafeBooleanValue(climateState, "smart_preconditioning");
        if (smartPreconditioning)
        {
            sb.AppendLine($"    • Precondizionamento Intelligente: Attivo");
        }
    }

    private static void ProcessMockDriveState(StringBuilder sb, JsonElement driveState)
    {
        var latitude = GetSafeDecimalValue(driveState, "latitude");
        var longitude = GetSafeDecimalValue(driveState, "longitude");

        if (latitude != 0 || longitude != 0)
        {
            sb.AppendLine($"    • Posizione GPS: {latitude:F6}, {longitude:F6}");
        }

        var heading = GetSafeIntValue(driveState, "heading");
        if (heading > 0)
        {
            sb.AppendLine($"    • Direzione: {heading}° {GetCompassDirection(heading)}");
        }

        if (driveState.TryGetProperty("speed", out var speedElement) &&
            speedElement.ValueKind != JsonValueKind.Null)
        {
            var speed = speedElement.GetInt32();
            sb.AppendLine($"    • Velocità: {speed} km/h");
        }
        else
        {
            sb.AppendLine($"    • Velocità: Fermo");
        }

        var gpsAsOf = GetSafeStringValue(driveState, "gps_as_of");
        if (!string.IsNullOrEmpty(gpsAsOf))
        {
            sb.AppendLine($"    • Ultimo Aggiornamento GPS: {gpsAsOf}");
        }

        var shiftState = GetSafeStringValue(driveState, "shift_state");
        if (!string.IsNullOrEmpty(shiftState))
        {
            sb.AppendLine($"    • Cambio: {TranslateShiftState(shiftState)}");
        }

        var power = GetSafeIntValue(driveState, "power");
        if (power != 0)
        {
            sb.AppendLine($"    • Potenza: {power} kW");
        }
    }

    private static void ProcessMockGuiSettings(StringBuilder sb, JsonElement guiSettings)
    {
        var distanceUnits = GetSafeStringValue(guiSettings, "gui_distance_units", "km/hr");
        var tempUnits = GetSafeStringValue(guiSettings, "gui_temperature_units", "C");
        var chargeRateUnits = GetSafeStringValue(guiSettings, "gui_charge_rate_units", "km/hr");
        var is24Hour = GetSafeBooleanValue(guiSettings, "gui_24_hour_time");
        var rangeDisplay = GetSafeStringValue(guiSettings, "gui_range_display", "Rated");

        sb.AppendLine($"    • Unità Distanza: {distanceUnits}");
        sb.AppendLine($"    • Unità Temperatura: {tempUnits}");
        sb.AppendLine($"    • Unità Velocità Ricarica: {chargeRateUnits}");
        sb.AppendLine($"    • Formato Ora: {(is24Hour ? "24 ore" : "12 ore")}");
        sb.AppendLine($"    • Display Autonomia: {rangeDisplay}");
    }

    #endregion

    #region Helper Methods

    // Metodi helper per gestire valori che potrebbero non esistere o essere null
    private static string GetSafeStringValue(JsonElement element, string propertyName, string defaultValue = "N/A")
    {
        return element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String ?
               prop.GetString() ?? defaultValue : defaultValue;
    }

    private static int GetSafeIntValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number ? prop.GetInt32() : 0;
    }

    private static decimal GetSafeDecimalValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.Number ? prop.GetDecimal() : 0m;
    }

    private static bool GetSafeBooleanValue(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var prop) &&
               prop.ValueKind == JsonValueKind.True;
    }

    private static string FormatJsonValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "null",
            JsonValueKind.Number => value.GetDecimal().ToString("F2"),
            JsonValueKind.True => "Sì",
            JsonValueKind.False => "No",
            JsonValueKind.Null => "N/A",
            JsonValueKind.Array => $"[{value.GetArrayLength()} elementi]",
            JsonValueKind.Object => "{oggetto complesso}",
            _ => value.GetRawText()
        };
    }

    private static string GetCompassDirection(int heading)
    {
        return heading switch
        {
            _ when heading >= 337 || heading <= 22 => "Nord",
            >= 23 and <= 67 => "Nord-Est",
            >= 68 and <= 112 => "Est",
            >= 113 and <= 157 => "Sud-Est",
            >= 158 and <= 202 => "Sud",
            >= 203 and <= 247 => "Sud-Ovest",
            >= 248 and <= 292 => "Ovest",
            >= 293 and <= 336 => "Nord-Ovest",
            _ => "Sconosciuto"
        };
    }

    private static string TranslateShiftState(string shiftState)
    {
        return shiftState?.ToUpper() switch
        {
            "P" => "Parcheggio",
            "D" => "Avanti",
            "R" => "Retromarcia",
            "N" => "Folle",
            null => "Non disponibile",
            _ => shiftState
        };
    }

    // Metodi di supporto per la categorizzazione (ORIGINALI)
    private static string GetCommandCategory(string commandName)
    {
        return commandName switch
        {
            var cmd when cmd.StartsWith("charge_") || cmd.Contains("charge") => "Ricarica",
            var cmd when cmd.StartsWith("door_") || cmd.Contains("trunk") => "Accesso Veicolo",
            var cmd when cmd.Contains("climate") || cmd.Contains("temp") || cmd.Contains("heat") || cmd.Contains("cool") || cmd.Contains("conditioning") => "Climatizzazione",
            var cmd when cmd.StartsWith("media_") || cmd.Contains("volume") => "Sistema Multimediale",
            var cmd when cmd.StartsWith("navigation_") => "Navigazione",
            var cmd when cmd.Contains("sentry") || cmd.Contains("valet") || cmd.Contains("speed_limit") || cmd.Contains("pin") => "Sicurezza",
            var cmd when cmd.Contains("seat") || cmd.Contains("steering_wheel") || cmd.Contains("window") || cmd.Contains("sun_roof") => "Comfort",
            var cmd when cmd.Contains("software") || cmd.Contains("schedule") => "Sistema",
            var cmd when cmd.Contains("lights") || cmd.Contains("horn") || cmd.Contains("homelink") || cmd.Contains("boombox") => "Funzioni Esterne",
            _ => "Altro"
        };
    }

    private static string GetCommandDisplayName(string commandName)
    {
        return commandName switch
        {
            "actuate_trunk" => "Apertura bagagliaio",
            "add_charge_schedule" => "Aggiunta programmazione ricarica",
            "add_precondition_schedule" => "Aggiunta programmazione precondizionamento",
            "adjust_volume" => "Regolazione volume",
            "auto_conditioning_start" => "Avvio climatizzazione automatica",
            "auto_conditioning_stop" => "Stop climatizzazione automatica",
            "cancel_software_update" => "Annulla aggiornamento software",
            "charge_max_range" => "Ricarica a massima autonomia",
            "charge_port_door_close" => "Chiusura sportello ricarica",
            "charge_port_door_open" => "Apertura sportello ricarica",
            "charge_standard" => "Ricarica standard",
            "charge_start" => "Avvio ricarica",
            "charge_stop" => "Stop ricarica",
            "door_lock" => "Blocco porte",
            "door_unlock" => "Sblocco porte",
            "flash_lights" => "Lampeggio luci",
            "honk_horn" => "Suono clacson",
            "guest_mode" => "Modalità ospite",
            "media_next_track" => "Traccia successiva",
            "media_prev_track" => "Traccia precedente",
            "media_toggle_playback" => "Play/Pausa",
            "navigation_request" => "Navigazione verso destinazione",
            "navigation_gps_request" => "Navigazione GPS",
            "set_charge_limit" => "Impostazione limite ricarica",
            "set_temps" => "Impostazione temperature",
            "set_sentry_mode" => "Modalità sentinella",
            "set_valet_mode" => "Modalità parcheggiatore",
            "set_vehicle_name" => "Impostazione nome veicolo",
            "window_control" => "Controllo finestrini",
            "remote_start_drive" => "Avvio remoto",
            "homelink_request" => "Comando HomeLink",
            "speed_limit_set_limit" => "Impostazione limite velocità",
            "speed_limit_activate" => "Attivazione limite velocità",
            "speed_limit_deactivate" => "Disattivazione limite velocità",
            "speed_limit_clear_pin" => "Cancellazione PIN limite velocità",
            "sun_roof_control" => "Controllo tetto apribile",
            "trigger_homelink" => "Attivazione HomeLink",
            "remote_seat_heater_request" => "Riscaldamento sedili",
            "remote_steering_wheel_heater_request" => "Riscaldamento volante",
            "set_scheduled_charging" => "Programmazione ricarica",
            "set_scheduled_departure" => "Programmazione partenza",
            "navigation_share_location" => "Condivisione posizione",
            "media_volume_up" => "Volume su",
            "media_volume_down" => "Volume giù",
            "boombox" => "Modalità Boombox",
            "schedule_software_update" => "Programmazione aggiornamento",
            "set_cabin_overheat_protection" => "Protezione surriscaldamento",
            "set_climate_keeper_mode" => "Modalità mantenimento clima",
            "set_preconditioning_max" => "Precondizionamento massimo",
            _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(commandName.Replace("_", " "))
        };
    }

    private static string FormatCommandParameters(string commandName, JsonElement parameters)
    {
        var paramList = new List<string>();

        foreach (var prop in parameters.EnumerateObject())
        {
            var value = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetDecimal().ToString("F1"),
                JsonValueKind.True => "Sì",
                JsonValueKind.False => "No",
                JsonValueKind.Array => $"[{string.Join(", ", prop.Value.EnumerateArray().Select(e => e.GetString()))}]",
                _ => prop.Value.ToString()
            };

            var displayName = prop.Name switch
            {
                "which_trunk" => "Bagagliaio",
                "start_time" => "Ora inizio",
                "end_time" => "Ora fine",
                "level" => "Livello",
                "percent" => "Percentuale",
                "enabled" => "Abilitato",
                "location" => "Destinazione",
                "latitude" => "Latitudine",
                "longitude" => "Longitudine",
                "vehicle_name" => "Nome veicolo",
                "on" => "Attivo",
                "state" => "Stato",
                "command" => "Comando",
                "days" => "Giorni",
                "time" => "Orario",
                "driver_temp" => "Temp. guidatore",
                "passenger_temp" => "Temp. passeggero",
                "heat_level" => "Livello riscaldamento",
                "seat_position" => "Posizione sedile",
                "limit_mph" => "Limite velocità",
                "pin" => "PIN",
                "lat" => "Latitudine",
                "lon" => "Longitudine",
                "address" => "Indirizzo",
                "volume" => "Volume",
                "media_type" => "Tipo media",
                "preset" => "Preset",
                "repeat" => "Ripeti",
                "shuffle" => "Casuale",
                "charging_amps" => "Ampere ricarica",
                "charging_sites" => "Stazioni ricarica",
                "departure_time" => "Orario partenza",
                "preconditioning_enabled" => "Precondizionamento",
                "preconditioning_weekdays_only" => "Solo giorni feriali",
                "off_peak_charging_enabled" => "Ricarica ore non di punta",
                "off_peak_charging_weekdays_only" => "Ricarica off-peak solo feriali",
                "end_time_charging" => "Fine ricarica",
                _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(prop.Name.Replace("_", " "))
            };

            paramList.Add($"{displayName}: {value}");
        }

        return paramList.Count != 0 ? string.Join(", ", paramList) : "";
    }

    #endregion
}