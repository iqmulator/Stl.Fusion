using System;
using Microsoft.Extensions.DependencyInjection;

namespace Stl.Fusion
{
    public static class ServiceCollectionExt
    {
        public static FusionBuilder AddFusion(this IServiceCollection services)
            => new(services);

        public static IServiceCollection AddFusion(this IServiceCollection services, Action<FusionBuilder> configureFusion)
        {
            var fusion = services.AddFusion();
            configureFusion.Invoke(fusion);
            return services;
        }
    }
}
