using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace ProyectoFarmaVita.Services.AuthorizationServices
{
    public class RoleAuthorizationService : IRoleAuthorizationService
    {
        private readonly AuthenticationStateProvider _authStateProvider;

        public RoleAuthorizationService(AuthenticationStateProvider authStateProvider)
        {
            _authStateProvider = authStateProvider;
        }

        public async Task<bool> HasRoleAsync(string requiredRole)
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
                return false;

            var userRole = authState.User.FindFirst(ClaimTypes.Role)?.Value;
            return string.Equals(userRole, requiredRole, StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> GetCurrentUserRoleAsync()
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity.IsAuthenticated)
                return string.Empty;

            return authState.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        }

        public async Task<bool> CanAccessPageAsync(string pageRole)
        {
            if (string.IsNullOrEmpty(pageRole))
                return true; // Sin restricción de rol

            // Soporte para múltiples roles separados por coma
            var requiredRoles = pageRole.Split(',')
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrEmpty(r))
                .ToArray();

            if (!requiredRoles.Any())
                return true;

            foreach (var role in requiredRoles)
            {
                if (await HasRoleAsync(role))
                    return true;
            }

            return false;
        }

        public async Task<List<string>> GetUserPermissionsAsync()
        {
            var userRole = await GetCurrentUserRoleAsync();

            Console.WriteLine($"🔍 Determinando permisos para rol: '{userRole}'");

            return userRole switch
            {
                "Administrador" => new List<string>
                {
                    "usuarios", "productos", "inventario", "ventas", "reportes",
                    "configuracion", "sucursales", "proveedores", "clientes", "roles"
                },
                "Gerente" => new List<string>
                {
                    "productos", "inventario", "ventas", "reportes",
                    "proveedores", "clientes", "usuarios", "sucursales"
                },
                "Cajero" => new List<string>
                {
                    "ventas", "clientes"
                },
                "Farmaceuta" => new List<string>
                {
                    "productos", "inventario", "ventas", "clientes", "proveedores"
                },
                "Vendedor" => new List<string>
                {
                    "ventas", "clientes"
                },
                _ => new List<string>()
            };
        }
    }
}