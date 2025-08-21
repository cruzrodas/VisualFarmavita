using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;

    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private DotNetObjectReference<CustomAuthenticationStateProvider>? _dotNetRef;
    private bool _isInitialized = false;
    private bool _disposed = false;
    private Timer? _tokenRenewalTimer;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public CustomAuthenticationStateProvider(
        IJSRuntime jsRuntime,
        NavigationManager navigationManager,
        ILogger<CustomAuthenticationStateProvider> logger)
    {
        _jsRuntime = jsRuntime;
        _navigationManager = navigationManager;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("⚠️ AuthenticationStateProvider ya fue disposed");
            return new AuthenticationState(_currentUser);
        }

        await _semaphore.WaitAsync();
        try
        {
            // Solo intentar obtener token si no estamos en prerendering
            if (_isInitialized)
            {
                await InitializeIfNeededAsync();
            }

            var authState = new AuthenticationState(_currentUser);
            _logger.LogInformation($"👤 Estado de autenticación: {_currentUser.Identity?.IsAuthenticated ?? false}");

            return authState;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando autenticación");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task InitializeIfNeededAsync()
    {
        if (_disposed) return;

        try
        {
            _logger.LogInformation("🔍 Obteniendo token del sessionStorage...");

            var token = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "authToken");

            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("✅ Token encontrado en sessionStorage");
                await ProcessTokenAsync(token);
            }
            else
            {
                _logger.LogInformation("ℹ️ No hay token en sessionStorage");
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("statically rendered"))
        {
            _logger.LogDebug("⏳ Esperando completar el renderizado antes de acceder a JavaScript");
            // No es un error real, solo necesitamos esperar
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo token del sessionStorage");
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    public async Task SetTokenAsync(string? token)
    {
        if (_disposed)
        {
            _logger.LogWarning("⚠️ Intento de SetToken en provider disposed");
            return;
        }

        await _semaphore.WaitAsync();
        try
        {
            _logger.LogInformation($"🔑 Estableciendo nuevo token de autenticación");

            if (string.IsNullOrEmpty(token))
            {
                // Limpiar autenticación
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

                try
                {
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "authToken");
                    _logger.LogInformation("🗑️ Token removido del sessionStorage");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Error removiendo token del sessionStorage");
                }

                StopTokenRenewalTimer();
            }
            else
            {
                // Establecer nueva autenticación
                await ProcessTokenAsync(token);

                try
                {
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "authToken", token);
                    _logger.LogInformation("💾 Token guardado en sessionStorage");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Error guardando token en sessionStorage");
                }

                StartTokenRenewalTimer(token);
            }

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
            _logger.LogInformation($"🔔 Estado de autenticación notificado: {_currentUser.Identity?.IsAuthenticated ?? false}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en SetTokenAsync");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ProcessTokenAsync(string token)
    {
        if (_disposed) return;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Verificar si el token ha expirado
            if (jwtToken.ValidTo < DateTime.UtcNow)
            {
                _logger.LogWarning("⚠️ Token expirado");
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                return;
            }

            // Extraer claims del token
            var claims = jwtToken.Claims.ToList();
            _logger.LogInformation($"📝 Claims extraídos: {claims.Count}");

            var identity = new ClaimsIdentity(claims, "jwt");
            _currentUser = new ClaimsPrincipal(identity);

            _logger.LogInformation($"✅ Usuario autenticado: {_currentUser.Identity?.Name ?? "Sin nombre"}");
            _logger.LogInformation($"👤 Rol: {_currentUser.FindFirst(ClaimTypes.Role)?.Value ?? "Sin rol"}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error procesando token JWT");
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }

    private void StartTokenRenewalTimer(string token)
    {
        if (_disposed) return;

        try
        {
            StopTokenRenewalTimer();

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var expiryTime = jwtToken.ValidTo;
            var renewalTime = expiryTime.AddMinutes(-5); // Renovar 5 minutos antes de expirar
            var delay = renewalTime - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                _tokenRenewalTimer = new Timer(async _ => await RenewTokenAsync(), null, delay, TimeSpan.FromMilliseconds(-1));
                _logger.LogInformation($"⏰ Timer de renovación configurado para: {renewalTime:HH:mm:ss}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error configurando timer de renovación");
        }
    }

    private async Task RenewTokenAsync()
    {
        if (_disposed) return;

        try
        {
            _logger.LogInformation("🔄 Renovando token...");
            // Aquí puedes implementar la lógica de renovación si la tienes
            // Por ahora, simplemente loggeamos que se intentó renovar
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error renovando token");
        }
    }

    private void StopTokenRenewalTimer()
    {
        if (_tokenRenewalTimer != null)
        {
            _tokenRenewalTimer.Dispose();
            _tokenRenewalTimer = null;
            _logger.LogDebug("⏹️ Timer de renovación de token detenido");
        }
    }

    // Método para inicializar después del primer render
    public async Task InitializeAsync()
    {
        if (_disposed) return;

        await _semaphore.WaitAsync();
        try
        {
            if (!_isInitialized)
            {
                _logger.LogInformation("🚀 Inicializando CustomAuthenticationStateProvider...");
                _isInitialized = true;

                // Intentar obtener el token del sessionStorage
                await InitializeIfNeededAsync();

                // Notificar el estado inicial
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en InitializeAsync");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _logger.LogInformation("🗑️ CustomAuthenticationStateProvider disposed");

        try
        {
            StopTokenRenewalTimer();

            _dotNetRef?.Dispose();
            _dotNetRef = null;

            _semaphore?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error durante dispose");
        }
    }
}