using DAL.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DAL.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            // Ensure database is created/migrated
            await context.Database.MigrateAsync();

            // Fix PostgreSQL sequences if they are out of sync
            await ResetSequenceAsync(context, "Categories", "category_id");
            await ResetSequenceAsync(context, "Providers", "provider_id");
            await ResetSequenceAsync(context, "Products", "product_id");
            await ResetSequenceAsync(context, "ProductVariants", "variant_id");

            // Seed Admin User
            var adminEmail = "admin@nongxanh.com";
            if (!await context.Users.AnyAsync(u => u.Email == adminEmail))
            {
                var adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    Email = adminEmail,
                    DisplayName = "System Admin",
                    Provider = "Local",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    // Password: "AdminPassword123!"
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("AdminPassword123!"), 
                    Role = UserRole.Admin 
                };
                
                await context.Users.AddAsync(adminUser);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded Admin user.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }

    private static async Task ResetSequenceAsync(ApplicationDbContext context, string tableName, string columnName)
    {
        try
        {
            // This block attempts to find the sequence associated with the column and reset it.
            // It handles both SERIAL sequences and IDENTITY sequences.
            var sql = $@"
DO $$
DECLARE
    seq_name text;
    max_id integer;
BEGIN
    -- Find the sequence name
    SELECT pg_get_serial_sequence('""{tableName}""', '{columnName}') INTO seq_name;
    
    -- If pg_get_serial_sequence returns null, try to guess the standard identity sequence name
    IF seq_name IS NULL THEN
        seq_name := '""{tableName}_{columnName}_seq""';
    END IF;

    -- Get the max id
    EXECUTE 'SELECT COALESCE(MAX(""{columnName}""), 0) FROM ""{tableName}""' INTO max_id;
    
    -- Reset the sequence
    IF seq_name IS NOT NULL THEN
        PERFORM setval(seq_name, max_id + 1, false);
    END IF;
END $$;
";
            await context.Database.ExecuteSqlRawAsync(sql);
            Console.WriteLine($"Attempted to reset sequence for {tableName}.{columnName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not reset sequence for {tableName}: {ex.Message}");
        }
    }
}
