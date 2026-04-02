using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using APITeamsV3.Domain.Entities;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddDbContext<SmartDbContext>(options =>
            options.UseSqlServer("Data Source=sqlserver;Initial Catalog=Smart;User ID=sa;Password=Password123;")); // Using default dev credentials
    })
    .Build();

using var scope = host.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<SmartDbContext>();

var code = "01819.26.00357";
var section = await context.Set<Seccion>().FirstOrDefaultAsync(s => s.Codigo == code || s.GrupoCodigo == code);

if (section != null)
{
    Console.WriteLine($"ID_SECCION_FOUND:{section.IdSeccion}");
}
else
{
    Console.WriteLine("ID_SECCION_NOT_FOUND");
}
