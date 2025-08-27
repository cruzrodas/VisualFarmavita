using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.LoginServices
{
    public class LoginService : ILoginService
    {
        private readonly string llavejwt;
        private readonly IDbContextFactory<FarmaDbContext> _dbContextFactory;

        // Cache estático para mejorar rendimiento
        private static readonly SHA256 _sha256 = SHA256.Create();
        private static readonly object _hashLock = new object();

        public LoginService(IConfiguration iConfiguration, IDbContextFactory<FarmaDbContext> dbContextFactory)
        {
            llavejwt = iConfiguration["llavejwt"];
            _dbContextFactory = dbContextFactory;
        }

        // DTO para evitar usar dynamic
        private class PersonaLoginDto
        {
            public int IdPersona { get; set; }
            public string Nombre { get; set; } = "";
            public string Apellido { get; set; } = "";
            public string Email { get; set; } = "";
            public string Contraseña { get; set; } = "";
            public int? IdSucursal { get; set; }
            public bool? Activo { get; set; }
            public string? RolTipo { get; set; }
        }

        #region Métodos de Hash Optimizados

        /// <summary>
        /// Hashea una contraseña usando SHA256 - VERSIÓN OPTIMIZADA
        /// </summary>
        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            try
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                byte[] hash;

                // Usar lock solo cuando sea necesario para thread safety
                lock (_hashLock)
                {
                    hash = _sha256.ComputeHash(bytes);
                }

                return Convert.ToBase64String(hash);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al hashear contraseña: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Verifica si una contraseña coincide con el hash - VERSIÓN OPTIMIZADA
        /// </summary>
        private bool VerifyPassword(string plainTextPassword, string hashedPassword)
        {
            if (string.IsNullOrEmpty(plainTextPassword) || string.IsNullOrEmpty(hashedPassword))
                return false;

            try
            {
                var hashOfInput = HashPassword(plainTextPassword);
                return string.Equals(hashOfInput, hashedPassword, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        public async Task<RespuestaAutenticacion> Login(CredencialesUsuario credencialesUsuario)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Configurar timeout más corto para evitar colgarse
            dbContext.Database.SetCommandTimeout(10); // 10 segundos máximo

            var respuestaAutenticacion = new RespuestaAutenticacion();

            try
            {
                // Validación rápida de entrada
                if (string.IsNullOrWhiteSpace(credencialesUsuario?.Email) ||
                    string.IsNullOrWhiteSpace(credencialesUsuario?.Password))
                {
                    respuestaAutenticacion.Error = "Email y contraseña son requeridos";
                    return respuestaAutenticacion;
                }

                // Query optimizada - usando DTO tipado en lugar de dynamic
                var persona = await dbContext.Persona
                    .AsNoTracking() // Mejor rendimiento, no tracking de cambios
                    .Include(p => p.IdRoolNavigation)
                    .Where(p => p.Email == credencialesUsuario.Email && p.Activo == true) // Filtrar inactivos en la query
                    .Select(p => new PersonaLoginDto // Proyección específica para reducir datos transferidos
                    {
                        IdPersona = p.IdPersona,
                        Nombre = p.Nombre ?? "",
                        Apellido = p.Apellido ?? "",
                        Email = p.Email ?? "",
                        Contraseña = p.Contraseña ?? "",
                        IdSucursal = p.IdSucursal,
                        Activo = p.Activo,
                        RolTipo = p.IdRoolNavigation != null ? p.IdRoolNavigation.TipoRol : null
                    })
                    .FirstOrDefaultAsync();

                if (persona == null)
                {
                    // Simular tiempo de hash para evitar timing attacks
                    HashPassword("dummy_password_to_prevent_timing_attack");
                    respuestaAutenticacion.Error = "Credenciales incorrectas";
                    return respuestaAutenticacion;
                }

                // Verificar contraseña directamente
                if (!VerifyPassword(credencialesUsuario.Password, persona.Contraseña))
                {
                    respuestaAutenticacion.Error = "Credenciales incorrectas";
                    return respuestaAutenticacion;
                }

                // Construir token con datos ya disponibles
                respuestaAutenticacion = ConstruirTokenOptimizado(persona);
                return respuestaAutenticacion;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en Login: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                respuestaAutenticacion.Error = "Error interno del servidor";
                return respuestaAutenticacion;
            }
        }

        private RespuestaAutenticacion ConstruirTokenOptimizado(PersonaLoginDto persona)
        {
            try
            {
                // Pre-construir claims de forma más eficiente
                var claims = new List<Claim>(5); // Pre-size para evitar re-allocations

                // Agregar email
                claims.Add(new Claim(ClaimTypes.Email, persona.Email));

                // Agregar nombre completo
                string nombreCompleto = $"{persona.Nombre} {persona.Apellido}".Trim();
                claims.Add(new Claim("nombre", nombreCompleto));

                // Agregar ID de persona
                claims.Add(new Claim("idPersona", persona.IdPersona.ToString()));

                // Agregar rol si existe
                if (!string.IsNullOrEmpty(persona.RolTipo))
                {
                    claims.Add(new Claim(ClaimTypes.Role, persona.RolTipo));
                }

                // Agregar ID de sucursal si existe
                if (persona.IdSucursal.HasValue)
                {
                    claims.Add(new Claim("idSucursal", persona.IdSucursal.Value.ToString()));
                }

                // Verificar llave JWT una sola vez
                if (string.IsNullOrEmpty(llavejwt))
                {
                    return new RespuestaAutenticacion { Error = "Error de configuración JWT" };
                }

                var keybuffer = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(llavejwt));
                DateTime expireTime = DateTime.UtcNow.AddMinutes(60); // Usar UTC para mejor rendimiento

                var token = new JwtSecurityToken(
                    issuer: null,
                    audience: null,
                    claims: claims,
                    expires: expireTime,
                    signingCredentials: new SigningCredentials(keybuffer, SecurityAlgorithms.HmacSha256)
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                return new RespuestaAutenticacion
                {
                    Token = tokenString,
                    Expiration = expireTime,
                    Email = persona.Email
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al construir token: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new RespuestaAutenticacion { Error = "Error al generar token" };
            }
        }

        public async Task<RespuestaAutenticacion> RenovarToken(string email)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();
                dbContext.Database.SetCommandTimeout(5); // Timeout más corto para renovación

                var persona = await dbContext.Persona
                    .AsNoTracking()
                    .Include(p => p.IdRoolNavigation)
                    .Where(p => p.Email == email && p.Activo == true)
                    .Select(p => new PersonaLoginDto
                    {
                        IdPersona = p.IdPersona,
                        Nombre = p.Nombre ?? "",
                        Apellido = p.Apellido ?? "",
                        Email = p.Email ?? "",
                        IdSucursal = p.IdSucursal,
                        RolTipo = p.IdRoolNavigation != null ? p.IdRoolNavigation.TipoRol : null
                    })
                    .FirstOrDefaultAsync();

                if (persona != null)
                {
                    return ConstruirTokenOptimizado(persona);
                }

                return new RespuestaAutenticacion { Error = "Usuario no encontrado" };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en RenovarToken: {ex.Message}");
                return new RespuestaAutenticacion { Error = "Error interno" };
            }
        }
    }
}