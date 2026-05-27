namespace AspNetCore.Api.Modules.Authorization.Services
{
    public interface IAuthorizationSeedService
    {
        Task SeedAsync(CancellationToken cancellationToken);
    }
}
