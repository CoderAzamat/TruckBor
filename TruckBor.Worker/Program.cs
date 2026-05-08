using Microsoft.EntityFrameworkCore;
using TruckBor.Application;
using TruckBor.Infrastructure;
using TruckBor.Infrastructure.Data;
using TruckBor.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLogging(b => b.AddConsole());
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var db  = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await db.Database.MigrateAsync();
        log.LogInformation("Worker DB ready");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Worker DB init failed");
    }
}

host.Run();
