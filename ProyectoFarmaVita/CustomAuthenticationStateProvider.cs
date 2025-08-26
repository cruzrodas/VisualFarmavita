using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ProyectoFarmaVita.Services.LoginServices
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
    {
        private readonly IJSRuntime _jsRuntime;
        private bool _disposed = false;
        private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

        public CustomAuthenticationStateProvider(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                // Durante prerendering, no podemos acceder al localStorage
                var token = await GetTokenAsync();

                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("🔍 No hay token - Usuario no autenticado");
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                    return new AuthenticationState(_currentUser);
                }

                var claims = ParseClaimsFromJwt(token);
                if (claims == null || !claims.Any())
                {
                    Console.WriteLine("🔍 Token inválido o sin claims");
                    await RemoveTokenAsync();
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                    return new AuthenticationState(_currentUser);
                }

                // Verificar si el token ha expirado
                var expiryClaim = claims.FirstOrDefault(x => x.Type == "exp");
                if (expiryClaim != null)
                {
                    var expiryDateUnix = long.Parse(expiryClaim.Value);
                    var expiryDate = DateTimeOffset.FromUnixTimeSeconds(expiryDateUnix);

                    if (expiryDate <= DateTimeOffset.UtcNow)
                    {
                        Console.WriteLine("🔍 Token expirado");
                        await RemoveTokenAsync();
                        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                        return new AuthenticationState(_currentUser);
                    }
                }

                var identity = new ClaimsIdentity(claims, "jwt");
                _currentUser = new ClaimsPrincipal(identity);

                Console.WriteLine($"✅ Usuario autenticado: {_currentUser.FindFirst("nombre")?.Value ?? "Sin nombre"}");
                Console.WriteLine($"✅ Rol: {_currentUser.FindFirst(ClaimTypes.Role)?.Value ?? "Sin rol"}");

                return new AuthenticationState(_currentUser);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
            {
                // Durante prerendering, retornamos usuario no autenticado
                Console.WriteLine("🔍 JSInterop no disponible durante prerendering - Usuario no autenticado");
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                return new AuthenticationState(_currentUser);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en GetAuthenticationStateAsync: {ex.Message}");
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                return new AuthenticationState(_currentUser);
            }
        }

        public async Task SetTokenAsync(string token)
        {
            try
            {
                Console.WriteLine("💾 Guardando token...");

                if (string.IsNullOrEmpty(token))
                {
                    await RemoveTokenAsync();
                    return;
                }

                // Solo intentar guardar si JSInterop está disponible
                try
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", token);
                    Console.WriteLine("✅ Token guardado en localStorage");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
                {
                    Console.WriteLine("⚠️ No se puede guardar en localStorage durante prerendering");
                    // Continuamos sin error - el token se guardará cuando JSInterop esté disponible
                }

                // Actualizar el estado de autenticación inmediatamente
                var claims = ParseClaimsFromJwt(token);
                if (claims != null && claims.Any())
                {
                    var identity = new ClaimsIdentity(claims, "jwt");
                    _currentUser = new ClaimsPrincipal(identity);

                    Console.WriteLine("✅ Estado de autenticación actualizado en memoria");
                    NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al guardar token: {ex.Message}");
            }
        }

        public async Task<string> GetTokenAsync()
        {
            try
            {
                // Verificar si JSInterop está disponible
                if (_jsRuntime is IJSInProcessRuntime)
                {
                    var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
                    return token ?? string.Empty;
                }
                else
                {
                    // Durante prerendering, JSInterop no está disponible
                    Console.WriteLine("🔍 JSInterop no disponible durante prerendering");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al obtener token: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task RemoveTokenAsync()
        {
            try
            {
                Console.WriteLine("🗑️ Removiendo token...");

                try
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
                    Console.WriteLine("✅ Token removido del localStorage");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("JavaScript interop"))
                {
                    Console.WriteLine("⚠️ No se puede acceder a localStorage durante prerendering");
                }

                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
                Console.WriteLine("✅ Estado de autenticación limpiado");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al remover token: {ex.Message}");
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("🚀 Inicializando AuthenticationStateProvider...");
                var authState = await GetAuthenticationStateAsync();
                NotifyAuthenticationStateChanged(Task.FromResult(authState));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al inicializar: {ex.Message}");
            }
        }

        public async Task LogoutAsync()
        {
            await RemoveTokenAsync();
        }

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(jwt);

                var claims = jsonToken.Claims.ToList();

                // Agregar claim de Name si no existe
                if (!claims.Any(c => c.Type == ClaimTypes.Name))
                {
                    var nombreClaim = claims.FirstOrDefault(c => c.Type == "nombre");
                    if (nombreClaim != null)
                    {
                        claims.Add(new Claim(ClaimTypes.Name, nombreClaim.Value));
                    }
                }

                Console.WriteLine($"🔍 Claims parseados: {claims.Count}");
                foreach (var claim in claims)
                {
                    Console.WriteLine($"   - {claim.Type}: {claim.Value}");
                }

                return claims;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al parsear JWT: {ex.Message}");
                return new List<Claim>();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}