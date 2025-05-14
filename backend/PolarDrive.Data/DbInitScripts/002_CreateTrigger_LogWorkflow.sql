CREATE TRIGGER IF NOT EXISTS trg_log_workflow_update
AFTER UPDATE ON TeslaWorkflows
FOR EACH ROW
BEGIN
    INSERT INTO TeslaWorkflowEvents
          (TeslaVehicleId, FieldChanged, OldValue, NewValue, EventTimestamp)
    SELECT NEW.TeslaVehicleId,
           'IsActiveFlag',
           OLD.IsActiveFlag,
           NEW.IsActiveFlag,
           CURRENT_TIMESTAMP
    WHERE OLD.IsActiveFlag <> NEW.IsActiveFlag;

    INSERT INTO TeslaWorkflowEvents
          (TeslaVehicleId, FieldChanged, OldValue, NewValue, EventTimestamp)
    SELECT NEW.TeslaVehicleId,
           'FetchDataFlag',
           OLD.FetchDataFlag,
           NEW.FetchDataFlag,
           CURRENT_TIMESTAMP
    WHERE OLD.FetchDataFlag <> NEW.FetchDataFlag;
END;