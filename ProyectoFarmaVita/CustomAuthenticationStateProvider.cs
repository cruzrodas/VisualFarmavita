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

    public CustomAuthenticationStateProvider(IJSRuntime jsRuntime, NavigationManager navigationManager, ILoginService loginService)
    {
        JsRuntime = jsRuntime;
        NavigationManager = navigationManager;
        LoginService = loginService;
        _firstRenderCompleted = false;
        _checkActivityTimer = new Timer(CheckActivityAndRenewToken, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [JSInvokable]
    public async Task SetWindowActiveStatus(bool isActive)
    {
        _isWindowActive = isActive;
        if (isActive && !IsLoginPage())
        {
            await CheckTokenExpiration();
        }
    }

    private async void CheckActivityAndRenewToken(object ob)
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
            await SetTokenAsync(null);
            await NavigateToCalendarAndReload();
        }
        else
        {
            StartRenewTokenTimer();
        }
    }

    private async Task<string> GetTokenFromSessionStorageAsync()
    {
        try
        {
            return await JsRuntime.InvokeAsync<string>("sessionStorage.getItem", "authToken");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private bool IsTokenExpired(string token)
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken jwtToken = handler.ReadToken(token) as JwtSecurityToken;
        string exp = jwtToken?.Claims.FirstOrDefault(claim => claim.Type == "exp")?.Value;
        if (exp != null && long.TryParse(exp, out var expUnix))
        {
            var expDateTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            return expDateTime < DateTime.UtcNow;
        }
        return true;
    }

    private async Task NavigateToCalendarAndReload()
    {
        await SetTokenAsync(null);
        NavigationManager.NavigateTo("/");
    }

    public async Task SetTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            await JsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "authToken");
            _authenticationState = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
        else
        {
            await JsRuntime.InvokeVoidAsync("sessionStorage.setItem", "authToken", token);
            IEnumerable<Claim> claims = ParseClaimsFromJWT(token);
            ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, "JWT");
            _authenticationState = new AuthenticationState(new ClaimsPrincipal(claimsIdentity));
        }
        NotifyAuthenticationStateChanged(Task.FromResult(_authenticationState));
        //NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void NotifyFirstRenderCompleted()
    {
        _firstRenderCompleted = true;
        if (!IsLoginPage() )
        {
            CheckTokenExpiration().ConfigureAwait(false);
        }
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string token = await GetTokenFromSessionStorageAsync();
        ClaimsIdentity identity = string.IsNullOrEmpty(token) || IsTokenExpired(token)
            ? new ClaimsIdentity()
            : new ClaimsIdentity(ParseClaimsFromJWT(token), "JWT");
        _authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
        return _authenticationState;
    }

    private IEnumerable<Claim> ParseClaimsFromJWT(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
        return keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()));
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
        string token = await GetTokenFromSessionStorageAsync();
        if (!string.IsNullOrEmpty(token))
        {
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            string email = jsonToken?.Claims.FirstOrDefault(claim => claim.Type == ClaimTypes.Email)?.Value;

            if (!string.IsNullOrEmpty(email))
            {
                RespuestaAutenticacion newTokenResponse = await LoginService.RenovarToken(email);
                if (newTokenResponse != null)
                {
                    await SetTokenAsync(newTokenResponse.Token);
                }
            }
        }
    }

    public void StartRenewTokenTimer()
    {
        _renewTokenTimer?.Dispose(); // Si ya existe un temporizador, lo eliminamos.
        _renewTokenTimer = new Timer(async _ => await RenewTokenIfNecessary(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    private async Task RenewTokenIfNecessary()
    {
        string token = await GetTokenFromSessionStorageAsync();
        if (!string.IsNullOrEmpty(token))
        {
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            JwtSecurityToken jwtToken = handler.ReadToken(token) as JwtSecurityToken;
            string expClaim = jwtToken?.Claims.FirstOrDefault(claim => claim.Type == "exp")?.Value;

            if (expClaim != null && long.TryParse(expClaim, out long expUnix))
            {
                DateTime expDateTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                TimeSpan timeRemaining = expDateTime - DateTime.UtcNow;
                // si faltan 5 minutos o menos para que expire el token, renovarlo.
                if (timeRemaining.TotalMinutes <= 5 && _isWindowActive /* && !IsLoginPage() && !IsResetPasswordPage()*/)
                {
                    await RenewToken();
                }
            }
        }
    }

    public void StopRenewTokenTimer()
    {
        _renewTokenTimer?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        StopRenewTokenTimer();
        _checkActivityTimer?.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().ConfigureAwait(false);
    }
    private bool IsLoginPage()
    {
        return NavigationManager.Uri.EndsWith("/login", StringComparison.OrdinalIgnoreCase);
    }
}
