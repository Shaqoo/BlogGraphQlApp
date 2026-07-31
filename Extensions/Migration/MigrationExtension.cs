using BlogGraphQlApp.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace BlogGraphQlApp.Extensions.Migration
{
    public static class MigrationExtension
    {
        public static async Task ApplyMigrationAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(MigrationExtension));

            logger.LogInformation("Starting database migration process...");

            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            logger.LogInformation("Resolved IDbContextFactory<AppDbContext> successfully.");

            await using var db = await factory.CreateDbContextAsync();
            logger.LogInformation("Created AppDbContext instance from factory.");

            try
            {
                logger.LogInformation("Ensuring database is created and accessible...");
                EnsureDbCreated(db, logger);

                logger.LogInformation("Running EF Core migrations...");
                await db.Database.MigrateAsync();

                logger.LogInformation("✅ Database migration completed successfully.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ An error occurred while applying database migrations.");
                throw;
            }
            finally
            {
                logger.LogInformation("Migration process finished. Cleaning up resources...");
            }
        }

        private static void EnsureDbCreated(AppDbContext dbContext, ILogger logger)
        {
            try
            {
                if (dbContext.Database.CanConnect())
                {
                    logger.LogInformation("Database already exists and connection is valid.");
                    return;
                }

                var dataBaseName = dbContext.Database.GetDbConnection().Database;
                var connectionString = dbContext.Database.GetConnectionString()!;
                logger.LogWarning("Database '{DatabaseName}' not found. Attempting to create it...", dataBaseName);


                var builder = new MySqlConnectionStringBuilder(connectionString)
                {
                    Database = "mysql"  
                };

                using var connection = new MySqlConnection(builder.ConnectionString);
                connection.Open();
                logger.LogInformation("Connected to MySQL server using system schema.");

                using var query = connection.CreateCommand();
                query.CommandText = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{dataBaseName}'";
                logger.LogDebug("Checking if schema '{DatabaseName}' exists...", dataBaseName);

                var exists = query.ExecuteScalar();
                if (exists == null)
                {
                    logger.LogInformation("Schema '{DatabaseName}' does not exist. Creating it now...", dataBaseName);
                    query.CommandText = $"CREATE DATABASE `{dataBaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                    query.ExecuteNonQuery();
                    logger.LogInformation("✅ Schema '{DatabaseName}' created successfully.", dataBaseName);
                }
                else
                {
                    logger.LogInformation("Schema '{DatabaseName}' already exists.", dataBaseName);
                }
                logger.LogInformation("Closing database connection...");
                connection.Close();

                connection.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Error while ensuring database exists.");
                throw;
            }
        }
    }
}
