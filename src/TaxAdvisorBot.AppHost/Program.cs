#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");

var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume("qdrant-data")
    .WithLifetime(ContainerLifetime.Persistent);

var web = builder.AddCSharpApp("web", "../platforms/TaxAdvisorBot.Web/TaxAdvisorBot.Web.csproj")
    .WithExternalHttpEndpoints()
    .WithReference(redis)
    .WithReference(qdrant);

builder.Build().Run();
