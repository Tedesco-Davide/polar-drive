-- SQL Server: Controlla se l'indice esiste prima di crearlo
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_outage_active_vehicle' AND object_id = OBJECT_ID('OutagePeriods'))
BEGIN
    CREATE INDEX idx_outage_active_vehicle 
    ON OutagePeriods (VehicleId, OutageEnd)
    WHERE OutageType = 'Outage Vehicle';
END