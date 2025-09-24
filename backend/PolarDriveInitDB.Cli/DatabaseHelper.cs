using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace PolarDrive.Data.Helpers;

/// <summary>
/// Helper per operazioni avanzate sui database
/// </summary>
public static class DatabaseHelper
{
    /// <summary>
    /// Forza la cancellazione del database chiudendo tutte le connessioni attive
    /// </summary>
    /// <param name="context">Il DbContext da utilizzare</param>
    /// <returns></returns>
    public static async Task ForceDeleteDatabaseAsync(DbContext context)
    {
        try
        {
            var connectionString = context.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Connection string is null or empty");
            }

            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;

            if (string.IsNullOrEmpty(databaseName))
            {
                throw new InvalidOperationException("Database name not found in connection string");
            }

            Console.WriteLine($"🔄 Force deleting database: {databaseName}");

            // Connessione al database master per eseguire i comandi di gestione
            builder.InitialCatalog = "master";
            var masterConnectionString = builder.ConnectionString;

            using var masterConnection = new SqlConnection(masterConnectionString);
            await masterConnection.OpenAsync();

            // Verifica se il database esiste
            var checkDbSql = $"SELECT DB_ID('{databaseName}')";
            using var checkCmd = new SqlCommand(checkDbSql, masterConnection);
            var dbId = await checkCmd.ExecuteScalarAsync();

            if (dbId == DBNull.Value || dbId == null)
            {
                Console.WriteLine($"ℹ️ Database {databaseName} does not exist, skipping deletion");
                return;
            }

            // 1. Termina tutte le connessioni attive al database
            var killConnectionsSql = $@"
                DECLARE @sql NVARCHAR(MAX) = '';
                SELECT @sql = @sql + 'KILL ' + CAST(session_id AS VARCHAR) + ';' + CHAR(13)
                FROM sys.dm_exec_sessions 
                WHERE database_id = DB_ID('{databaseName}') 
                AND session_id != @@SPID;
                
                IF @sql != ''
                BEGIN
                    PRINT 'Killing active connections...';
                    EXEC sp_executesql @sql;
                END";

            using var killCmd = new SqlCommand(killConnectionsSql, masterConnection);
            await killCmd.ExecuteNonQueryAsync();

            // 2. Imposta il database in modalità SINGLE_USER per prevenire nuove connessioni
            var setSingleUserSql = $@"
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                PRINT 'Database set to SINGLE_USER mode';";

            using var setSingleUserCmd = new SqlCommand(setSingleUserSql, masterConnection);
            await setSingleUserCmd.ExecuteNonQueryAsync();

            // 3. Elimina il database
            var dropDbSql = $@"
                DROP DATABASE [{databaseName}];
                PRINT 'Database dropped successfully';";

            using var dropDbCmd = new SqlCommand(dropDbSql, masterConnection);
            await dropDbCmd.ExecuteNonQueryAsync();

            Console.WriteLine($"✅ Successfully force deleted database: {databaseName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Warning: Could not force delete database: {ex.Message}");
            Console.WriteLine("🔄 Falling back to standard EnsureDeleted method...");
            
            // Fallback al metodo standard
            await context.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>
    /// Verifica se il database esiste
    /// </summary>
    /// <param name="context">Il DbContext da utilizzare</param>
    /// <returns>True se il database esiste</returns>
    public static async Task<bool> DatabaseExistsAsync(DbContext context)
    {
        try
        {
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Ottiene informazioni sulle connessioni attive al database
    /// </summary>
    /// <param name="context">Il DbContext da utilizzare</param>
    /// <returns>Numero di connessioni attive</returns>
    public static async Task<int> GetActiveConnectionsCountAsync(DbContext context)
    {
        try
        {
            var connectionString = context.Database.GetConnectionString();
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;

            builder.InitialCatalog = "master";
            var masterConnectionString = builder.ConnectionString;

            using var masterConnection = new SqlConnection(masterConnectionString);
            await masterConnection.OpenAsync();

            var sql = $@"
                SELECT COUNT(*) 
                FROM sys.dm_exec_sessions 
                WHERE database_id = DB_ID('{databaseName}')";

            using var cmd = new SqlCommand(sql, masterConnection);
            var result = await cmd.ExecuteScalarAsync();
            
            return result != null ? Convert.ToInt32(result) : 0;
        }
        catch
        {
            return -1; // Indica errore nel conteggio
        }
    }
}