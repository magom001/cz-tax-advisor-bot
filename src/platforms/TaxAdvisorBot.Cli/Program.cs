using Microsoft.Extensions.Hosting;
using TaxAdvisorBot.Application;
using TaxAdvisorBot.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationOptions();
builder.AddInfrastructureServices();

using var host = builder.Build();
await host.RunAsync();
