using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaxAdvisorBot.Infrastructure.AI;

namespace TaxAdvisorBot.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddRedisDistributedCache("cache");

        builder.Services.AddSemanticKernel();

        return builder;
    }
}
