using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MathLearning.Infrastructure.Persistance;

namespace MathLearning.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            return services;
        }

        public static IServiceCollection AddApiDatabase(this IServiceCollection services, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("Default");

            services.AddDbContext<ApiDbContext>(opt =>
                opt.UseNpgsql(
                    connectionString,
                    npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

            return services;
        }

        public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration config)
        {
            var connectionString = config.GetConnectionString("Default");

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseNpgsql(connectionString));

            return services;
        }
    }
}
