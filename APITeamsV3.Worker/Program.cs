using APITeamsV3.Worker;
using APITeamsV3.Infrastructure;
using APITeamsV3.Application;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<SyncWorker>();

var host = builder.Build();
host.Run();
