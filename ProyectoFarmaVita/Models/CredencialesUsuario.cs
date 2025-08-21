using System.ComponentModel.DataAnnotations;

namespace ProyectoFarmaVita.Models
{
    public class CredencialesUsuario
    {
        [Required(ErrorMessage = "El email es obligatorio.")]
        [EmailAddress(ErrorMessage = "Email no es válido.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Password { get; set; }
    }
}
