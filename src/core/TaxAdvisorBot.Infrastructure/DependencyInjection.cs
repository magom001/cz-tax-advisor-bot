using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TaxAdvisorBot.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddRedisDistributedCache("cache");

        // Service registrations will be added in later tasks.
        return builder;
    }
}
