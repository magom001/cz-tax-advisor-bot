using Microsoft.Extensions.Hosting;
using TaxAdvisorBot.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructureServices();

using var host = builder.Build();
await host.RunAsync();
