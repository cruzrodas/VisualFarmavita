using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.LoginServices
{
    public interface ILoginService
    {
        Task<RespuestaAutenticacion> Login(CredencialesUsuario credencialesUsuario);
        Task<RespuestaAutenticacion> RenovarToken(string email);
    }
}
