using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
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

        public async Task<RespuestaAutenticacion> Login(CredencialesUsuario credencialesUsuario)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var fechaHoy = DateTime.Now;
            var respuestaAutenticacion = new RespuestaAutenticacion();

            // Buscar persona por email que esté activa
            var persona = await dbContext.Persona
                .Include(p => p.IdRoolNavigation) // Incluir el rol
                .FirstOrDefaultAsync(p => p.Email == credencialesUsuario.Email && p.Activo == true);

            if (persona == null)
            {
                respuestaAutenticacion.Error = "Login incorrecto";
                return respuestaAutenticacion;
            }

            // Verificar contraseña
            if (VerifyPassword(credencialesUsuario.Password, persona.Contraseña))
            {
                respuestaAutenticacion = ConstruirToken(persona);
                return respuestaAutenticacion;
            }
            else
            {
                respuestaAutenticacion.Error = "Login incorrecto";
                return respuestaAutenticacion;
            }
        }

        private bool VerifyPassword(string plainTextPassword, string hashedPassword)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(plainTextPassword);
            var hash = sha256.ComputeHash(bytes);
            var enteredPasswordHash = Convert.ToBase64String(hash);

            return hashedPassword == enteredPasswordHash;
        }

        private RespuestaAutenticacion ConstruirToken(Persona persona)
        {
            List<Claim> claims = new List<Claim>();

            // Agregar rol si existe
            if (persona.IdRoolNavigation != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, persona.IdRoolNavigation.TipoRol ?? "Usuario"));
            }

            // Agregar email
            claims.Add(new Claim(ClaimTypes.Email, persona.Email ?? ""));

            // Agregar nombre completo
            string nombreCompleto = $"{persona.Nombre} {persona.Apellido}".Trim();
            claims.Add(new Claim("nombre", nombreCompleto));

            // Agregar ID de persona
            claims.Add(new Claim("idPersona", persona.IdPersona.ToString()));

            // Agregar ID de sucursal si existe
            if (persona.IdSucursal.HasValue)
            {
                claims.Add(new Claim("idSucursal", persona.IdSucursal.Value.ToString()));
            }

            // IdentityModelEventSource.ShowPII = true;
            var keybuffer = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(llavejwt));
            DateTime expireTime = DateTime.Now.AddMinutes(60);

            JwtSecurityToken token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                claims: claims,
                expires: expireTime,
                signingCredentials: new SigningCredentials(keybuffer, SecurityAlgorithms.HmacSha256)
            );

            return new RespuestaAutenticacion()
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                Expiration = expireTime,
                Email = persona.Email
            };
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