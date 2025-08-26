using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ProyectoFarmaVita.Services.LoginServices
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ProtectedLocalStorage _localStorage;
        private readonly ILogger<CustomAuthenticationStateProvider> _logger;
        private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        private const string TOKEN_KEY = "authToken";

        public CustomAuthenticationStateProvider(ProtectedLocalStorage localStorage,
                                               ILogger<CustomAuthenticationStateProvider> logger)
        {
            _localStorage = localStorage;
            _logger = logger;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                Console.WriteLine("🔍 GetAuthenticationStateAsync - Iniciando...");

                var token = await GetTokenAsync();

                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("❌ Token no encontrado o vacío");
                    return new AuthenticationState(_anonymous);
                }

                Console.WriteLine($"🎟️ Token encontrado: {token.Substring(0, Math.Min(50, token.Length))}...");

                var identity = GetClaimsIdentityFromToken(token);

                if (identity == null || !identity.IsAuthenticated)
                {
                    Console.WriteLine("❌ Token inválido o expirado");
                    await RemoveTokenAsync();
                    return new AuthenticationState(_anonymous);
                }

                var user = new ClaimsPrincipal(identity);
                var userName = user.FindFirst("nombre")?.Value ??
                              user.FindFirst(ClaimTypes.Email)?.Value ??
                              "Usuario desconocido";

                Console.WriteLine($"✅ Usuario autenticado: {userName}");

                return new AuthenticationState(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting authentication state");
                Console.WriteLine($"❌ Error en GetAuthenticationStateAsync: {ex.Message}");
                return new AuthenticationState(_anonymous);
            }
        }

        public async Task SetTokenAsync(string token)
        {
            try
            {
                Console.WriteLine("💾 Guardando token...");

                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("❌ Token vacío, no se guardará");
                    return;
                }

                await _localStorage.SetAsync(TOKEN_KEY, token);
                Console.WriteLine("✅ Token guardado exitosamente");

                // Verificar que se guardó correctamente
                var savedToken = await GetTokenAsync();
                if (savedToken == token)
                {
                    Console.WriteLine("✅ Token verificado correctamente");

                    // Crear identity y notificar cambio
                    var identity = GetClaimsIdentityFromToken(token);
                    if (identity != null && identity.IsAuthenticated)
                    {
                        var authState = new AuthenticationState(new ClaimsPrincipal(identity));
                        NotifyAuthenticationStateChanged(Task.FromResult(authState));
                        Console.WriteLine("✅ Estado de autenticación actualizado");
                    }
                }
                else
                {
                    Console.WriteLine("❌ Error: el token no se guardó correctamente");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting token");
                Console.WriteLine($"❌ Error al guardar token: {ex.Message}");
            }
        }

        public async Task<string> GetTokenAsync()
        {
            try
            {
                var result = await _localStorage.GetAsync<string>(TOKEN_KEY);
                return result.Success ? result.Value : string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al obtener token: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                Console.WriteLine("🚪 Cerrando sesión...");
                await RemoveTokenAsync();

                var authState = new AuthenticationState(_anonymous);
                NotifyAuthenticationStateChanged(Task.FromResult(authState));
                Console.WriteLine("✅ Sesión cerrada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                Console.WriteLine($"❌ Error al cerrar sesión: {ex.Message}");
            }
        }

        private async Task RemoveTokenAsync()
        {
            try
            {
                await _localStorage.DeleteAsync(TOKEN_KEY);
                Console.WriteLine("✅ Token eliminado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al eliminar token: {ex.Message}");
            }
        }

        private ClaimsIdentity GetClaimsIdentityFromToken(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("❌ Token vacío en GetClaimsIdentityFromToken");
                    return null;
                }

                var tokenHandler = new JwtSecurityTokenHandler();

                // Verificar que el token tiene el formato correcto
                if (!tokenHandler.CanReadToken(token))
                {
                    Console.WriteLine("❌ Token con formato inválido");
                    return null;
                }

                var jwtToken = tokenHandler.ReadJwtToken(token);

                // Verificar expiración
                if (jwtToken.ValidTo < DateTime.UtcNow)
                {
                    Console.WriteLine($"❌ Token expirado. Expira: {jwtToken.ValidTo}, Actual: {DateTime.UtcNow}");
                    return null;
                }

                var claims = jwtToken.Claims.ToList();
                Console.WriteLine($"🎫 Claims encontrados: {claims.Count}");

                foreach (var claim in claims)
                {
                    Console.WriteLine($"   - {claim.Type}: {claim.Value}");
                }

                return new ClaimsIdentity(claims, "jwt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al procesar token: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var authState = await GetAuthenticationStateAsync();
            return authState.User.Identity?.IsAuthenticated ?? false;
        }
    }
}