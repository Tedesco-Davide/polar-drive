using Microsoft.EntityFrameworkCore;

namespace PolarDrive.Data.DbContexts;

public static class DbInitHelper
{
    public static async Task RunDbInitScriptsAsync(PolarDriveDbContext context)
    {
        var baseDir = AppContext.BaseDirectory;
        var scriptsPath = Path.Combine(baseDir, "DbInitScripts");
        
        Console.WriteLine($"  📁 Looking for scripts in: {scriptsPath}");
        
        if (!Directory.Exists(scriptsPath))
        {
            // Prova anche nella directory corrente
            scriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "DbInitScripts");
            Console.WriteLine($"  📁 Alternative path: {scriptsPath}");
        }
        
        if (!Directory.Exists(scriptsPath))
        {
            Console.WriteLine("  ⚠️  DbInitScripts folder not found (optional).");
            return;
        }

        var sqlFiles = Directory.GetFiles(scriptsPath, "*.sql")
                                .OrderBy(f => f)
                                .ToList();
        
        Console.WriteLine($"  📝 Found {sqlFiles.Count} SQL scripts to execute");
        
        foreach (var file in sqlFiles)
        {
            var fileName = Path.GetFileName(file);
            Console.WriteLine($"  ▶️  Executing: {fileName}");
            
            var sql = await File.ReadAllTextAsync(file);
            
            // Split by GO statements
            var batches = sql.Split(
                new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO", "\nGO", "GO\r\n", "GO\n" }, 
                StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var batch in batches)
            {
                if (!string.IsNullOrWhiteSpace(batch))
                {
                    await context.Database.ExecuteSqlRawAsync(batch);
                }
            }
            
            Console.WriteLine($"  ✅ {fileName} executed successfully");
        }
    }
}