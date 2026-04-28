#pragma warning disable ASPIRECSHARPAPPS001

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache");

var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume("qdrant-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mongo = builder.AddMongoDB("mongodb")
    .WithDataVolume("mongo-data")
    .WithLifetime(ContainerLifetime.Persistent);

var mongoDB = mongo.AddDatabase("taxadvisor");

var web = builder.AddCSharpApp("web", "../platforms/TaxAdvisorBot.Web/TaxAdvisorBot.Web.csproj")
    .WithExternalHttpEndpoints()
    .WithReference(redis)
    .WithReference(qdrant)
    .WithReference(mongoDB)
    .WaitFor(redis)
    .WaitFor(qdrant)
    .WaitFor(mongo);

builder.Build().Run();
