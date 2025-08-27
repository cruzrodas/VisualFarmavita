using Microsoft.AspNetCore.Components.Authorization;

namespace ProyectoFarmaVita.Services.AuthorizationServices
{
    public interface IRoleAuthorizationService
    {
        Task<bool> HasRoleAsync(string requiredRole);
        Task<string> GetCurrentUserRoleAsync();
        Task<bool> CanAccessPageAsync(string pageRole);
        Task<List<string>> GetUserPermissionsAsync();
    }
}