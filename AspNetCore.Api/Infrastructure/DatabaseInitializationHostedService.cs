using AspNetCore.Api.Modules.Authorization.Services;
using AspNetCore.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Infrastructure
{
    public sealed class DatabaseInitializationHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public DatabaseInitializationHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);

            var seedService = scope.ServiceProvider.GetRequiredService<IAuthorizationSeedService>();
            await seedService.SeedAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
