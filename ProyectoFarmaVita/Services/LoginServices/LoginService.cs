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
                Console.WriteLine($"🔧 === DEBUG HASH PASSWORD ===");
                Console.WriteLine($"🔧 Hasheando password: '{password}'");

                if (string.IsNullOrEmpty(password))
                {
                    Console.WriteLine($"❌ Password vacío o null");
                    return string.Empty;
                }

                using var sha256 = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(password);
                Console.WriteLine($"🔧 Password length: {password.Length}");
                Console.WriteLine($"🔧 Bytes originales count: {bytes.Length}");
                Console.WriteLine($"🔧 Primeros 10 bytes: [{string.Join(", ", bytes.Take(10))}]");

                var hash = sha256.ComputeHash(bytes);
                Console.WriteLine($"🔧 Hash bytes count: {hash.Length}");
                Console.WriteLine($"🔧 Primeros 10 hash bytes: [{string.Join(", ", hash.Take(10))}]");

                var result = Convert.ToBase64String(hash);
                Console.WriteLine($"🔧 Resultado Base64: '{result}'");
                Console.WriteLine($"🔧 Resultado Base64 length: {result.Length}");
                Console.WriteLine($"🔧 === FIN DEBUG HASH PASSWORD ===");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al hashear contraseña: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
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
                Console.WriteLine($"🔍 === DEBUG VERIFICACIÓN CONTRASEÑA ===");
                Console.WriteLine($"🔍 Password texto plano: '{plainTextPassword}'");
                Console.WriteLine($"🔍 Password texto plano length: {plainTextPassword?.Length ?? 0}");
                Console.WriteLine($"🔍 Hash desde BD: '{hashedPassword}'");
                Console.WriteLine($"🔍 Hash desde BD length: {hashedPassword?.Length ?? 0}");

                if (string.IsNullOrEmpty(plainTextPassword) || string.IsNullOrEmpty(hashedPassword))
                {
                    Console.WriteLine($"❌ Valores vacíos - Plain: {string.IsNullOrEmpty(plainTextPassword)}, Hash: {string.IsNullOrEmpty(hashedPassword)}");
                    Console.WriteLine($"🔍 === FIN DEBUG VERIFICACIÓN (VALORES VACÍOS) ===");
                    return false;
                }

                // Verificar si hay caracteres extraños o espacios
                Console.WriteLine($"🔍 Hash BD empieza con: '{hashedPassword.Substring(0, Math.Min(10, hashedPassword.Length))}'");
                Console.WriteLine($"🔍 Hash BD termina con: '{hashedPassword.Substring(Math.Max(0, hashedPassword.Length - 10))}'");
                Console.WriteLine($"🔍 ¿Hash BD tiene espacios al inicio? {hashedPassword.StartsWith(" ")}");
                Console.WriteLine($"🔍 ¿Hash BD tiene espacios al final? {hashedPassword.EndsWith(" ")}");

                var hashOfInput = HashPassword(plainTextPassword);
                Console.WriteLine($"🔍 Hash calculado: '{hashOfInput}'");
                Console.WriteLine($"🔍 Hash calculado length: {hashOfInput?.Length ?? 0}");

                if (!string.IsNullOrEmpty(hashOfInput) && !string.IsNullOrEmpty(hashedPassword))
                {
                    Console.WriteLine($"🔍 Hash calculado empieza con: '{hashOfInput.Substring(0, Math.Min(10, hashOfInput.Length))}'");
                    Console.WriteLine($"🔍 Hash calculado termina con: '{hashOfInput.Substring(Math.Max(0, hashOfInput.Length - 10))}'");
                }

                // Comparación exacta
                bool sonIguales = string.Equals(hashOfInput, hashedPassword, StringComparison.Ordinal);
                Console.WriteLine($"🔍 ¿Son exactamente iguales? {sonIguales}");

                // Comparación con trim (por si hay espacios)
                bool sonIgualesTrim = string.Equals(hashOfInput?.Trim(), hashedPassword?.Trim(), StringComparison.Ordinal);
                Console.WriteLine($"🔍 ¿Son iguales con Trim? {sonIgualesTrim}");

                // Comparación ignorando case (por si acaso)
                bool sonIgualesIgnoreCase = string.Equals(hashOfInput, hashedPassword, StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"🔍 ¿Son iguales ignorando case? {sonIgualesIgnoreCase}");

                // Verificar caracteres específicos
                if (!sonIguales && !string.IsNullOrEmpty(hashOfInput) && !string.IsNullOrEmpty(hashedPassword))
                {
                    Console.WriteLine($"🔍 === ANÁLISIS DETALLADO DE DIFERENCIAS ===");
                    int minLength = Math.Min(hashOfInput.Length, hashedPassword.Length);
                    for (int i = 0; i < minLength && i < 20; i++) // Solo primeros 20 caracteres
                    {
                        if (hashOfInput[i] != hashedPassword[i])
                        {
                            Console.WriteLine($"🔍 Diferencia en posición {i}: calculado='{hashOfInput[i]}' ({(int)hashOfInput[i]}), BD='{hashedPassword[i]}' ({(int)hashedPassword[i]})");
                        }
                    }
                    Console.WriteLine($"🔍 === FIN ANÁLISIS DETALLADO ===");
                }

                Console.WriteLine($"🔍 === FIN DEBUG VERIFICACIÓN (RESULTADO: {sonIguales}) ===");

                return sonIguales;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error al verificar contraseña: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        #endregion

        public async Task<RespuestaAutenticacion> Login(CredencialesUsuario credencialesUsuario)
        {
            Console.WriteLine("🚀 ===== INICIO LOGIN DEBUG =====");
            Console.WriteLine($"📧 Email recibido: '{credencialesUsuario.Email}'");
            Console.WriteLine($"🔑 Password recibido: '{credencialesUsuario.Password}'");
            Console.WriteLine($"🔑 Password recibido length: {credencialesUsuario.Password?.Length ?? 0}");

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
                Console.WriteLine($"🔍 Buscando usuario con email: '{credencialesUsuario.Email}'");

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
                Console.WriteLine($"   - Contraseña Hash: '{persona.Contraseña}'");
                Console.WriteLine($"   - Contraseña Hash Length: {persona.Contraseña?.Length ?? 0}");

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
                        Console.WriteLine($"🎟️ Token: {respuestaAutenticacion.Token.Substring(0, Math.Min(50, respuestaAutenticacion.Token.Length))}...");
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

                    // DEBUG ADICIONAL: Probar con contraseñas comunes para debug
                    Console.WriteLine("🔧 === DEBUG: PROBANDO CONTRASEÑAS COMUNES ===");
                    string[] passwordsComunes = { "admin", "123456", "password", "farmaceuta", "123", "admin123" };

                    foreach (var testPassword in passwordsComunes)
                    {
                        var testHash = HashPassword(testPassword);
                        bool testMatch = string.Equals(testHash, persona.Contraseña, StringComparison.Ordinal);
                        Console.WriteLine($"🔧 Test '{testPassword}' -> Hash: '{testHash}' -> Match: {testMatch}");
                        if (testMatch)
                        {
                            Console.WriteLine($"🎯 ¡ENCONTRADA! La contraseña original es: '{testPassword}'");
                            break;
                        }
                    }
                    Console.WriteLine("🔧 === FIN DEBUG CONTRASEÑAS COMUNES ===");

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