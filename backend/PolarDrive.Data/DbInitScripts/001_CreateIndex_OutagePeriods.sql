CREATE INDEX IF NOT EXISTS idx_outage_active_vehicle 
ON OutagePeriods (VehicleId, OutageEnd)
WHERE OutageType = 'Outage Vehicle';