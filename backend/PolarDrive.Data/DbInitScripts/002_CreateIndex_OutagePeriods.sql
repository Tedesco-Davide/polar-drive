CREATE INDEX IF NOT EXISTS idx_outage_active_vehicle 
ON OutagePeriods (TeslaVehicleId, OutageEnd)
WHERE OutageType = 'Outage Vehicle';