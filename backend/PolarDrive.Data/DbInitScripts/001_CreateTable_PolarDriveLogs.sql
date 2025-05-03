CREATE TABLE IF NOT EXISTS PolarDriveLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Source TEXT NOT NULL,         -- Es: "InitDB.Cli", "Anonymizer", "Scheduler"
    Level TEXT NOT NULL,          -- Es: "INFO", "WARNING", "ERROR"
    Message TEXT NOT NULL,        -- Contenuto del log
    Details TEXT                  -- (opzionale) stack trace, JSON, ecc.
);
