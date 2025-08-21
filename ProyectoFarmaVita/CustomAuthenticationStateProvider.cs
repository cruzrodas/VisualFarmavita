using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using ProyectoFarmaVita.Models;
using ProyectoFarmaVita.Services.LoginServices;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider, IDisposable, IAsyncDisposable
{
    private readonly IJSRuntime JsRuntime;
    private readonly NavigationManager NavigationManager;
    private readonly ILoginService LoginService;
    private AuthenticationState _authenticationState;
    private bool _firstRenderCompleted;
    private Timer _renewTokenTimer;
    private bool _isWindowActive = true;
    private Timer _checkActivityTimer;
    private DateTime _lastTokenCheck = DateTime.MinValue;
    private DotNetObjectReference<CustomAuthenticationStateProvider>? _dotNetHelper;
    private bool _isInitialized = false;

    public CustomAuthenticationStateProvider(IJSRuntime jsRuntime, NavigationManager navigationManager, ILoginService loginService)
    {
        JsRuntime = jsRuntime;
        NavigationManager = navigationManager;
        LoginService = loginService;
        _firstRenderCompleted = false;
        _checkActivityTimer = new Timer(CheckActivityAndRenewToken, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

        // Crear la referencia DotNet para JavaScript
        _dotNetHelper = DotNetObjectReference.Create(this);

        // Inicializar después de un pequeño delay para asegurar que JS esté disponible
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000); // Esperar 1 segundo
            await InitializeJavaScriptHelper();
        });
    }

    // Método para inicializar el helper de JavaScript
    private async Task InitializeJavaScriptHelper()
    {
        if (_isInitialized || _dotNetHelper == null) return;

        try
        {
            await JsRuntime.InvokeVoidAsync("setDotNetHelper", _dotNetHelper);
            _isInitialized = true;
            NotifyFirstRenderCompleted();
            Console.WriteLine("✅ DotNet helper inicializado correctamente");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error inicializando DotNet helper: {ex.Message}");
            // Reintentar después de 2 segundos
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                await InitializeJavaScriptHelper();
            });
        }
    }

    [JSInvokable]
    public async Task SetWindowActiveStatus(bool isActive)
    {
        _isWindowActive = isActive;
        Console.WriteLine($"🔄 Ventana activa: {isActive}");

        if (isActive && !IsLoginPage())
        {
            await CheckTokenExpiration();
        }
    }

    private async void CheckActivityAndRenewToken(object? state)
    {
        if (_isWindowActive && !IsLoginPage())
        {
            await RenewTokenIfNecessary();
        }
    }

    public async Task CheckTokenExpiration()
    {
        // Si no han pasado 5 minutos desde la última verificación, no hacer nada.
        if ((DateTime.UtcNow - _lastTokenCheck) < TimeSpan.FromMinutes(5))
        {
            return; // Evitar verificaciones frecuentes.
        }

        _lastTokenCheck = DateTime.UtcNow;
        var token = await GetTokenFromSessionStorageAsync();

        if (string.IsNullOrEmpty(token) || IsTokenExpired(token))
        {
            Console.WriteLine("🔒 Token expirado o no válido");
            await SetTokenAsync(null);

            if (!IsLoginPage())
            {
                Console.WriteLine("🔄 Redirigiendo al login...");
                NavigationManager.NavigateTo("/login", true);
            }
        }
        else
        {
            Console.WriteLine("✅ Token válido");
            StartRenewTokenTimer();
        }
    }

    private async Task<string?> GetTokenFromSessionStorageAsync()
    {
        try
        {
            return await JsRuntime.InvokeAsync<string>("sessionStorage.getItem", "authToken");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error obteniendo token del sessionStorage: {ex.Message}");
            return null;
        }
    }

    private bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
            var exp = jwtToken?.Claims.FirstOrDefault(claim => claim.Type == "exp")?.Value;

            if (exp != null && long.TryParse(exp, out var expUnix))
            {
                var expDateTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                var isExpired = expDateTime < DateTime.UtcNow;

                if (isExpired)
                {
                    Console.WriteLine($"⏰ Token expira en: {expDateTime} (UTC), ahora es: {DateTime.UtcNow} (UTC)");
                }

                return isExpired;
            }
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error verificando expiración del token: {ex.Message}");
            return true;
        }
    }

    public async Task SetTokenAsync(string? token)
    {
        try
        {
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("🔒 Limpiando token de autenticación");
                await JsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "authToken");
                _authenticationState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
            else
            {
                Console.WriteLine("🔑 Estableciendo nuevo token de autenticación");
                await JsRuntime.InvokeVoidAsync("sessionStorage.setItem", "authToken", token);
                var claims = ParseClaimsFromJWT(token);
                var claimsIdentity = new ClaimsIdentity(claims, "JWT");
                _authenticationState = new AuthenticationState(new ClaimsPrincipal(claimsIdentity));
            }

            NotifyAuthenticationStateChanged(Task.FromResult(_authenticationState));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error estableciendo token: {ex.Message}");
        }
    }

    public void NotifyFirstRenderCompleted()
    {
        _firstRenderCompleted = true;
        Console.WriteLine("✅ Primer render completado");

        if (!IsLoginPage())
        {
            _ = Task.Run(async () => await CheckTokenExpiration());
        }
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await GetTokenFromSessionStorageAsync();
            ClaimsIdentity identity;

            if (string.IsNullOrEmpty(token) || IsTokenExpired(token))
            {
                identity = new ClaimsIdentity();
                Console.WriteLine("❌ Usuario no autenticado");
            }
            else
            {
                identity = new ClaimsIdentity(ParseClaimsFromJWT(token), "JWT");
                Console.WriteLine("✅ Usuario autenticado");
            }

            _authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
            return _authenticationState;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en GetAuthenticationStateAsync: {ex.Message}");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    private IEnumerable<Claim> ParseClaimsFromJWT(string jwt)
    {
        try
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs != null)
            {
                var claims = keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString() ?? "")).ToList();
                Console.WriteLine($"🔍 Claims extraídos: {claims.Count}");
                return claims;
            }

            return new List<Claim>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error parseando claims del JWT: {ex.Message}");
            return new List<Claim>();
        }
    }

    private byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    private async Task RenewToken()
    {
        try
        {
            var token = await GetTokenFromSessionStorageAsync();
            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                var email = jsonToken?.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Email)?.Value;

                if (!string.IsNullOrEmpty(email))
                {
                    Console.WriteLine($"🔄 Renovando token para: {email}");
                    var newTokenResponse = await LoginService.RenovarToken(email);

                    if (newTokenResponse?.Token != null)
                    {
                        Console.WriteLine("✅ Token renovado exitosamente");
                        await SetTokenAsync(newTokenResponse.Token);
                    }
                    else
                    {
                        Console.WriteLine("❌ Error renovando token");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error renovando token: {ex.Message}");
        }
    }

    public void StartRenewTokenTimer()
    {
        _renewTokenTimer?.Dispose();
        _renewTokenTimer = new Timer(async _ => await RenewTokenIfNecessary(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        Console.WriteLine("⏰ Timer de renovación de token iniciado");
    }

    private async Task RenewTokenIfNecessary()
    {
        try
        {
            var token = await GetTokenFromSessionStorageAsync();
            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                var expClaim = jwtToken?.Claims.FirstOrDefault(claim => claim.Type == "exp")?.Value;

                if (expClaim != null && long.TryParse(expClaim, out long expUnix))
                {
                    var expDateTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                    var timeRemaining = expDateTime - DateTime.UtcNow;

                    // Si faltan 5 minutos o menos para que expire el token, renovarlo.
                    if (timeRemaining.TotalMinutes <= 5 && _isWindowActive)
                    {
                        Console.WriteLine($"⚠️ Token expira en {timeRemaining.TotalMinutes:F1} minutos, renovando...");
                        await RenewToken();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error en RenewTokenIfNecessary: {ex.Message}");
        }
    }

    public void StopRenewTokenTimer()
    {
        _renewTokenTimer?.Dispose();
        Console.WriteLine("⏹️ Timer de renovación de token detenido");
    }

    private bool IsLoginPage()
    {
        return NavigationManager.Uri.EndsWith("/login", StringComparison.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        StopRenewTokenTimer();
        _checkActivityTimer?.Dispose();
        _dotNetHelper?.Dispose();
        await Task.CompletedTask;
        Console.WriteLine("🗑️ CustomAuthenticationStateProvider disposed");
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
    }
}