using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using ProyectoFarmaVita.Components;
using ProyectoFarmaVita.Models;
using ProyectoFarmaVita.Services.AperturaCajaServices;
using ProyectoFarmaVita.Services.AsignacionTurnoServices;
using ProyectoFarmaVita.Services.AuthorizationServices;
using ProyectoFarmaVita.Services.CajaServices;
using ProyectoFarmaVita.Services.CategoriaProductoService;
using ProyectoFarmaVita.Services.CategoriaServices;
using ProyectoFarmaVita.Services.DepartamentoServices;
using ProyectoFarmaVita.Services.DetalleOrdenResServices;
using ProyectoFarmaVita.Services.DireccionServices;
using ProyectoFarmaVita.Services.EstadoCivilServices;
using ProyectoFarmaVita.Services.EstadoServices;
using ProyectoFarmaVita.Services.GeneroServices;
using ProyectoFarmaVita.Services.InventarioService;
using ProyectoFarmaVita.Services.LoginServices;
using ProyectoFarmaVita.Services.MunicipioService;
using ProyectoFarmaVita.Services.MunicipioServices;
using ProyectoFarmaVita.Services.OrdenRestablecimientoServices;
using ProyectoFarmaVita.Services.PersonaServices;
using ProyectoFarmaVita.Services.ProductoService;
using ProyectoFarmaVita.Services.ProductoServices;
using ProyectoFarmaVita.Services.ProveedorServices;
using ProyectoFarmaVita.Services.SucursalServices;
using ProyectoFarmaVita.Services.TelefonoServices;
using ProyectoFarmaVita.Services.TrasladoService;
using ProyectoFarmaVita.Services.TurnoTrabajoService;
using ProyectoFarmaVita.Services.TurnoTrabajoServices;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add authentication services with custom paths
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Login"; // Tu ruta de login personalizada
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });

// Add authorization services
builder.Services.AddAuthorization();

// Registro de servicios de la aplicación
builder.Services.AddTransient<IPersonaService, SPersonaServices>();
builder.Services.AddTransient<ISucursalService, SSucursalService>();
builder.Services.AddTransient<IEstadoCivil, SEstadoCivil>();
builder.Services.AddTransient<IGeneroServices, SGeneroServices>();
builder.Services.AddTransient<IdepartamentoService, SDepartamentoService>();
builder.Services.AddTransient<IMunicipioService, SMunicipioService>();
builder.Services.AddScoped<ITelefonoService, STelefonoService>();
builder.Services.AddScoped<IDireccionService, SDireccionService>();
builder.Services.AddScoped<ITurnoTrabajoService, STurnoTrabajoService>();
builder.Services.AddScoped<IAsignacionTurnoService, SAsignacionTurnoService>();
builder.Services.AddScoped<ICategoriaService, SCategoriaService>();
builder.Services.AddScoped<IProveedorService, SProveedorService>();
builder.Services.AddScoped<IProductoService, SProductoService>();
builder.Services.AddScoped<IInventarioService, SInventarioService>();
builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IRoleAuthorizationService, RoleAuthorizationService>();
builder.Services.AddScoped<IEstadoService, SEstadoService>();
builder.Services.AddScoped<ITrasladoService, STrasladoService>();
builder.Services.AddScoped<ICajaService, CajaService>();
builder.Services.AddScoped<IAperturaCajaService, AperturaCajaService>();
builder.Services.AddScoped<IDetalleOrdenResService, SDetalleOrdenResService>();
builder.Services.AddScoped<IOrdenRestablecimientoService, SOrdenRestablecimientoService>();




// MudBlazor
builder.Services.AddMudServices();

// Registro del DbContext
builder.Services.AddDbContextFactory<FarmaDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptions => sqlServerOptions.EnableRetryOnFailure());
    options.EnableSensitiveDataLogging(true);
    options.UseLazyLoadingProxies(false);
}, ServiceLifetime.Singleton);

// Registro de CustomAuthenticationStateProvider
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<CustomAuthenticationStateProvider>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();