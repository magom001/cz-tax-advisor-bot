using Microsoft.Extensions.DependencyInjection;

namespace TaxAdvisorBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Service registrations will be added in later tasks.
        return services;
    }
}
