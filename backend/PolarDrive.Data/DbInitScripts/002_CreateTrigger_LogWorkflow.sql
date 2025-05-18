CREATE TRIGGER IF NOT EXISTS trg_log_workflow_update
AFTER UPDATE ON Workflows
FOR EACH ROW
BEGIN
    INSERT INTO WorkflowEvents
          (VehicleId, FieldChanged, OldValue, NewValue, EventTimestamp)
    SELECT NEW.VehicleId,
           'IsActiveFlag',
           OLD.IsActiveFlag,
           NEW.IsActiveFlag,
           CURRENT_TIMESTAMP
    WHERE OLD.IsActiveFlag <> NEW.IsActiveFlag;

    INSERT INTO WorkflowEvents
          (VehicleId, FieldChanged, OldValue, NewValue, EventTimestamp)
    SELECT NEW.VehicleId,
           'FetchDataFlag',
           OLD.FetchDataFlag,
           NEW.FetchDataFlag,
           CURRENT_TIMESTAMP
    WHERE OLD.FetchDataFlag <> NEW.FetchDataFlag;
END;