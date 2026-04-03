using Microsoft.EntityFrameworkCore;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using System;
using System.Linq;

var builder = new DbContextOptionsBuilder<CentralDbContext>();
// Using the path relative to the root where the script will run
builder.UseSqlite("Data Source=APITeamsV3.API/APITeamsV3_Central.db");

using var context = new CentralDbContext(builder.Options);

try 
{
    Console.WriteLine("Iniciando reparación de historial de migraciones...");

    // 1. Inserción de la migración en conflicto (ya aplicada manualmente por ALTER TABLE previo)
    string migrationName = "20260402030000_AddApiClientColumns";
    string productVersion = "8.0.0"; // Versión aproximada de EF Core 8
    
    // Verificar si ya existe para evitar errores
    var exists = context.Database.SqlQueryRaw<string>("SELECT MigrationId FROM __EFMigrationsHistory WHERE MigrationId = {0}", migrationName).ToList().Any();

    if (!exists)
    {
        Console.WriteLine($"Sincronizando: {migrationName}");
        context.Database.ExecuteSqlRaw("INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1})", migrationName, productVersion);
        Console.WriteLine("¡Sincronización de AddApiClientColumns completada!");
    }
    else 
    {
        Console.WriteLine("La migración AddApiClientColumns ya está en el historial.");
    }

    // 2. Intentar aplicar las migraciones pendientes genuinas (como AddPilotMode)
    Console.WriteLine("Aplicando migraciones pendientes (incluyendo AddPilotMode)...");
    context.Database.Migrate();
    Console.WriteLine("¡Base de datos actualizada correctamente!");

}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    if (ex.InnerException != null) Console.Error.WriteLine($"INNER: {ex.InnerException.Message}");
}
