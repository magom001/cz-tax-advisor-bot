using TaxAdvisorBot.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddInfrastructureServices();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapGet("/", () => "TaxAdvisorBot Web");

app.Run();
