using Microsoft.EntityFrameworkCore;

namespace PolarDrive.Data.DbContexts;

public static class DbInitHelper
{
    public static async Task RunDbInitScriptsAsync(PolarDriveDbContext dbContext)
    {
        string basePath = AppContext.BaseDirectory;
        string scriptsPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PolarDrive.Data", "DbInitScripts"));

        if (!Directory.Exists(scriptsPath))
        {
            Console.WriteLine("❌ DbInitScripts folder not found.");
            return;
        }

        var sqlFiles = Directory.GetFiles(scriptsPath, "*.sql")
                                .OrderBy(f => f)
                                .ToList();

        foreach (var file in sqlFiles)
        {
            string sql = await File.ReadAllTextAsync(file);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                string fileName = Path.GetFileName(file);
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync(sql);
                    Console.WriteLine($"✅ Executed: {fileName}");

                    await dbContext.Database.ExecuteSqlRawAsync(@"
                        INSERT INTO PolarDriveLogs (Source, Level, Message) 
                        VALUES ({0}, {1}, {2})",
                        "InitDB.Cli",
                        "INFO",
                        $"Executed script: {fileName}"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error in {fileName}: {ex.Message}");

                    try
                    {
                        await dbContext.Database.ExecuteSqlRawAsync(@"
                            INSERT INTO PolarDriveLogs (Source, Level, Message, Details) 
                            VALUES ({0}, {1}, {2}, {3})",
                            "InitDB.Cli",
                            "ERROR",
                            $"Failed to execute: {fileName}",
                            ex.ToString()
                        );
                    }
                    catch
                    {
                        // Silenzia errori secondari
                    }
                }
            }
        }
    }
}
