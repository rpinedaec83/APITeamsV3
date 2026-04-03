using System;
using System.Linq;
using System.Threading.Tasks;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using APITeamsV3.Domain.Entities;
using System.Collections.Generic;

namespace APITeamsV3.Diagnostics
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddDbContext<SmartDbContext>(options =>
                        options.UseSqlServer("Data Source=ROBERTOP-LAP\\SQLEXPRESS;Initial Catalog=APITeamsDB;Integrated Security=True;TrustServerCertificate=True;"));
                })
                .Build();

            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmartDbContext>();

            string groupId = "cb3e921e-6fd4-4779-97f9-11334e878b38";
            Console.WriteLine($"--- Checking Team {groupId} ---");

            var team = await db.TeamsEquipos.FirstOrDefaultAsync(t => t.IdTeamsGroup == groupId);
            if (team == null)
            {
                Console.WriteLine("Team record not found in TeamsEquipos.");
                return;
            }

            Console.WriteLine($"Section ID: {team.IdSeccionSmart}");
            Console.WriteLine($"P1 (DB): {team.Propietario1}");
            Console.WriteLine($"P2 (DB): {team.Propietario2}");
            Console.WriteLine($"P3 (DB): {team.Propietario3}");
            Console.WriteLine($"P4 (DB): {team.Propietario4}");

            Console.WriteLine("\n--- Checking Programacion General ---");
            var prog = await db.TeamsProgramacionGeneral.FirstOrDefaultAsync(p => p.IdCurso == team.IdSeccionSmart);
            if (prog != null)
            {
                Console.WriteLine($"EmailFacilitador IN PROG: {prog.EmailFacilitador}");
                Console.WriteLine($"IdSede: {prog.IdSede}, IdUnidadNegocio: {prog.IdUnidadNegocio}");
            }
            else
            {
                Console.WriteLine("Programacion General not found for this section.");
            }

            Console.WriteLine("\n--- Checking Operational Logs (Last 10) ---");
            var logs = await db.TeamsLogOperativo
                .Where(l => l.Referencia == groupId || l.Referencia == team.IdSeccionSmart.ToString())
                .OrderByDescending(l => l.Fecha)
                .Take(10)
                .ToListAsync();

            foreach (var log in logs)
            {
                Console.WriteLine($"[{log.Fecha}] [{log.Tipo}] {log.Mensaje}");
            }
        }
    }
}
