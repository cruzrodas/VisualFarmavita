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

        public LoginService(IConfiguration iConfiguration, IDbContextFactory<FarmaDbContext> dbContextFactory)
        {
            llavejwt = iConfiguration["llavejwt"];
            _dbContextFactory = dbContextFactory;
        }

        #region Métodos de Hash (iguales que en PersonaService)

        /// <summary>
        /// Hashea una contraseña usando SHA256 (mismo método que PersonaService)
        /// </summary>
        private string HashPassword(string password)
        {
            try
            {
                if (string.IsNullOrEmpty(password))
                    return string.Empty;

                using var sha256 = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al hashear contraseña: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Verifica si una contraseña coincide con el hash
        /// </summary>
        private bool VerifyPassword(string plainTextPassword, string hashedPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(plainTextPassword) || string.IsNullOrEmpty(hashedPassword))
                    return false;

                var hashOfInput = HashPassword(plainTextPassword);
                return string.Equals(hashOfInput, hashedPassword, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar contraseña: {ex.Message}");
                return false;
            }
        }

        #endregion

        public async Task<RespuestaAutenticacion> Login(CredencialesUsuario credencialesUsuario)
        {
            Console.WriteLine("🚀 ===== INICIO LOGIN DEBUG =====");
            Console.WriteLine($"📧 Email recibido: '{credencialesUsuario.Email}'");
            Console.WriteLine($"🔑 Password recibido: '{credencialesUsuario.Password}'");

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var respuestaAutenticacion = new RespuestaAutenticacion();

            try
            {
                // 1. Verificar conexión a BD
                Console.WriteLine("🔍 Verificando conexión a BD...");
                var canConnect = await dbContext.Database.CanConnectAsync();
                Console.WriteLine($"🔗 Conexión BD: {canConnect}");

                if (!canConnect)
                {
                    Console.WriteLine("❌ No se puede conectar a la base de datos");
                    respuestaAutenticacion.Error = "Error de conexión a la base de datos";
                    return respuestaAutenticacion;
                }

                // 2. Buscar usuario por email
                Console.WriteLine($"🔍 Buscando usuario con email: {credencialesUsuario.Email}");

                var persona = await dbContext.Persona
                    .Include(p => p.IdRoolNavigation)
                    .Where(p => p.Email == credencialesUsuario.Email)
                    .FirstOrDefaultAsync();

                if (persona == null)
                {
                    Console.WriteLine("❌ Usuario no encontrado en la base de datos");
                    respuestaAutenticacion.Error = "Usuario no encontrado";
                    return respuestaAutenticacion;
                }

                Console.WriteLine($"✅ Usuario encontrado:");
                Console.WriteLine($"   - ID: {persona.IdPersona}");
                Console.WriteLine($"   - Nombre: {persona.Nombre} {persona.Apellido}");
                Console.WriteLine($"   - Email: {persona.Email}");
                Console.WriteLine($"   - Activo: {persona.Activo}");
                Console.WriteLine($"   - Rol: {persona.IdRoolNavigation?.TipoRol ?? "Sin rol"}");

                // 3. Verificar si está activo
                if (persona.Activo != true)
                {
                    Console.WriteLine("❌ Usuario inactivo");
                    respuestaAutenticacion.Error = "Usuario inactivo";
                    return respuestaAutenticacion;
                }

                // 4. Verificar contraseña
                Console.WriteLine("🔍 Verificando contraseña...");

                // Usar el método VerifyPassword que usa el mismo hash que PersonaService
                bool passwordMatch = VerifyPassword(credencialesUsuario.Password, persona.Contraseña);
                Console.WriteLine($"🔍 Contraseña correcta: {passwordMatch}");

                if (passwordMatch)
                {
                    Console.WriteLine("✅ Contraseña correcta - Generando token");

                    // 5. Construir token
                    Console.WriteLine("🎟️ Construyendo token...");
                    respuestaAutenticacion = ConstruirToken(persona);

                    if (!string.IsNullOrEmpty(respuestaAutenticacion.Token))
                    {
                        Console.WriteLine("✅ Token generado exitosamente");
                        Console.WriteLine($"🎟️ Token: {respuestaAutenticacion.Token.Substring(0, 50)}...");
                    }
                    else
                    {
                        Console.WriteLine("❌ Error al generar token");
                        respuestaAutenticacion.Error = "Error al generar token";
                    }

                    return respuestaAutenticacion;
                }
                else
                {
                    Console.WriteLine("❌ Contraseña incorrecta");
                    respuestaAutenticacion.Error = "Credenciales incorrectas";
                    return respuestaAutenticacion;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 ERROR CRÍTICO: {ex.Message}");
                Console.WriteLine($"💥 Stack Trace: {ex.StackTrace}");
                respuestaAutenticacion.Error = $"Error interno: {ex.Message}";
                return respuestaAutenticacion;
            }
            finally
            {
                Console.WriteLine("🏁 ===== FIN LOGIN DEBUG =====");
            }
        }

        private RespuestaAutenticacion ConstruirToken(Persona persona)
        {
            try
            {
                Console.WriteLine("🔧 Iniciando construcción de token...");

                List<Claim> claims = new List<Claim>();

                // Agregar rol si existe
                if (persona.IdRoolNavigation != null)
                {
                    string rol = persona.IdRoolNavigation.TipoRol ?? "Usuario";
                    claims.Add(new Claim(ClaimTypes.Role, rol));
                    Console.WriteLine($"👤 Rol agregado: {rol}");
                }
                else
                {
                    Console.WriteLine("⚠️ Sin rol asignado");
                }

                // Agregar email
                claims.Add(new Claim(ClaimTypes.Email, persona.Email ?? ""));
                Console.WriteLine($"📧 Email agregado: {persona.Email}");

                // Agregar nombre completo
                string nombreCompleto = $"{persona.Nombre} {persona.Apellido}".Trim();
                claims.Add(new Claim("nombre", nombreCompleto));
                Console.WriteLine($"👤 Nombre agregado: {nombreCompleto}");

                // Agregar ID de persona
                claims.Add(new Claim("idPersona", persona.IdPersona.ToString()));
                Console.WriteLine($"🆔 ID Persona agregado: {persona.IdPersona}");

                // Agregar ID de sucursal si existe
                if (persona.IdSucursal.HasValue)
                {
                    claims.Add(new Claim("idSucursal", persona.IdSucursal.Value.ToString()));
                    Console.WriteLine($"🏢 ID Sucursal agregado: {persona.IdSucursal.Value}");
                }

                // Verificar llave JWT
                if (string.IsNullOrEmpty(llavejwt))
                {
                    Console.WriteLine("❌ Llave JWT no configurada");
                    return new RespuestaAutenticacion { Error = "Error de configuración JWT" };
                }

                Console.WriteLine($"🔑 Llave JWT configurada (longitud: {llavejwt.Length})");

                var keybuffer = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(llavejwt));
                DateTime expireTime = DateTime.Now.AddMinutes(60);

                JwtSecurityToken token = new JwtSecurityToken(
                    issuer: null,
                    audience: null,
                    claims: claims,
                    expires: expireTime,
                    signingCredentials: new SigningCredentials(keybuffer, SecurityAlgorithms.HmacSha256)
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
                Console.WriteLine("✅ Token JWT creado exitosamente");

                return new RespuestaAutenticacion()
                {
                    Token = tokenString,
                    Expiration = expireTime,
                    Email = persona.Email
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al construir token: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return new RespuestaAutenticacion { Error = $"Error al generar token: {ex.Message}" };
            }
        }

        public async Task<RespuestaAutenticacion> RenovarToken(string email)
        {
            try
            {
                using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                var persona = await dbContext.Persona
                    .Include(p => p.IdRoolNavigation)
                    .FirstOrDefaultAsync(p => p.Email == email && p.Activo == true);

                if (persona != null)
                {
                    return ConstruirToken(persona);
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en RenovarToken: {ex.Message}");
                return null;
            }
        }
    }
}