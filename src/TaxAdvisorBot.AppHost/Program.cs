#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

var web = builder.AddCSharpApp("web", "../platforms/TaxAdvisorBot.Web/TaxAdvisorBot.Web.csproj")
    .WithHttpEndpoint(targetPort: 5000, name: "http")
    .WithExternalHttpEndpoints();

builder.Build().Run();
