namespace ProyectoFarmaVita.Models
{
    public class RespuestaAutenticacion
    {
        public string? Token { get; set; }
        public DateTime Expiration { get; set; }
        public string? Email { get; set; }
        public string? Error { get; set; }
        public string? NombreCompleto { get; set; }
        public string? Rol { get; set; }
    }
}