using Microsoft.EntityFrameworkCore;
using PolarDrive.Data.Entities;

namespace PolarDrive.Data.DbContexts;

public static class DbInitHelper
{
    public static async Task RunDbInitScriptsAsync(PolarDriveDbContext dbContext)
    {
        var logger = new PolarDriveLogger(dbContext);

        string basePath = AppContext.BaseDirectory;
        string scriptsPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "PolarDrive.Data", "DbInitScripts"));

        if (!Directory.Exists(scriptsPath))
        {
            Console.WriteLine("❌ DbInitScripts folder not found.");
            await logger.Error("DbInitHelper.RunDbInitScriptsAsync", "Scripts folder not found", $"Path attempted: {scriptsPath}");
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

                    await logger.Info(
                        "DbInitHelper.RunDbInitScriptsAsync",
                        $"Executed script: {fileName}"
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error in {fileName}: {ex.Message}");

                    await logger.Error(
                        "DbInitHelper.RunDbInitScriptsAsync",
                        $"Failed to execute script: {fileName}",
                        ex.ToString()
                    );
                }
            }
        }
    }
}