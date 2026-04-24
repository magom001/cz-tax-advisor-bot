#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");

var web = builder.AddCSharpApp("web", "../platforms/TaxAdvisorBot.Web/TaxAdvisorBot.Web.csproj")
    .WithExternalHttpEndpoints()
    .WithReference(redis);

builder.Build().Run();
