using Microsoft.EntityFrameworkCore;

namespace PolarDrive.Data.DbContexts;

public static class DbInitHelper
{
    public static void RunDbInitScripts(PolarDriveDbContext dbContext)
    {
        string basePath = AppContext.BaseDirectory;
        string scriptsPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PolarDrive.Data", "DbInitScripts"));

        if (!Directory.Exists(scriptsPath))
        {
            Console.WriteLine("❌ DbInitScripts folder not found.");
            return;
        }

        var sqlFiles = Directory.GetFiles(scriptsPath, "*.sql").OrderBy(f => f);

        foreach (var file in sqlFiles)
        {
            string sql = File.ReadAllText(file);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                string fileName = Path.GetFileName(file);
                try
                {
                    dbContext.Database.ExecuteSqlRaw(sql);
                    Console.WriteLine($"✅ Executed: {fileName}");

                    // NON loggare nella tabella log se stai creando proprio la tabella log!
                    if (!fileName.Contains("PolarDriveLogs", StringComparison.OrdinalIgnoreCase))
                    {
                        dbContext.Database.ExecuteSqlRaw(@"
                            INSERT INTO PolarDriveLogs (Source, Level, Message) 
                            VALUES ({0}, {1}, {2})",
                            "InitDB.Cli",
                            "INFO",
                            $"Executed script: {fileName}"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error in {fileName}: {ex.Message}");

                    try
                    {
                        // Tenta il log solo se la tabella esiste già
                        if (!fileName.Contains("PolarDriveLogs", StringComparison.OrdinalIgnoreCase))
                        {
                            dbContext.Database.ExecuteSqlRaw(@"
                                INSERT INTO PolarDriveLogs (Source, Level, Message, Details) 
                                VALUES ({0}, {1}, {2}, {3})",
                                "InitDB.Cli",
                                "ERROR",
                                $"Failed to execute: {fileName}",
                                ex.ToString()
                            );
                        }
                    }
                    catch
                    {
                        // Silenzia eventuali errori secondari di log
                    }
                }
            }
        }
    }
}
