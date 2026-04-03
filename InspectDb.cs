using Microsoft.EntityFrameworkCore;
using APITeamsV3.Infrastructure.Persistence.Contexts;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;

var builder = new DbContextOptionsBuilder<CentralDbContext>();
builder.UseSqlite("Data Source=APITeamsV3.API/APITeamsV3_Central.db");

using var context = new CentralDbContext(builder.Options);
var applied = context.Database.GetAppliedMigrations();
Console.WriteLine("Applied Migrations:");
foreach (var m in applied) Console.WriteLine(m);

var pending = context.Database.GetPendingMigrations();
Console.WriteLine("\nPending Migrations:");
foreach (var m in pending) Console.WriteLine(m);
