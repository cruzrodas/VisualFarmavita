using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.AperturaCajaServices
{
    public interface IAperturaCajaService
    {
        Task<bool> AddUpdateAsync(AperturaCaja aperturaCaja);
        Task<bool> DeleteAsync(int idAperturaCaja);
        Task<List<AperturaCaja>> GetAllAsync();
        Task<AperturaCaja> GetByIdAsync(int idAperturaCaja);
        Task<MPaginatedResult<AperturaCaja>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true);

        // Métodos específicos para apertura/cierre de caja
        Task<int?> AbrirCajaAsync(int idCaja, int idPersona, double montoApertura, string observaciones = "");
        Task<bool> CerrarCajaAsync(int idAperturaCaja, double montoCierre, string observaciones = "");
        Task<AperturaCaja?> GetAperturaCajaActivaAsync(int idCaja);
        Task<List<AperturaCaja>> GetAperturasCajaActivasAsync();
        Task<bool> TieneCajaAbiertaAsync(int idPersona);
        Task<AperturaCaja?> GetCajaAbiertaPorPersonaAsync(int idPersona);

        // Consultas específicas
        Task<List<AperturaCaja>> GetByPersonaAsync(int idPersona);
        Task<List<AperturaCaja>> GetByCajaAsync(int idCaja);
        Task<List<AperturaCaja>> GetByFechaAsync(DateTime fecha);
        Task<List<AperturaCaja>> GetByRangoFechasAsync(DateTime fechaInicio, DateTime fechaFin);

        // Reportes y estadísticas
        Task<Dictionary<string, object>> GetEstadisticasCajaAsync(int idCaja, DateTime? fechaInicio = null, DateTime? fechaFin = null);
        Task<List<dynamic>> GetResumenCajasAsync(DateTime? fecha = null);
        Task<bool> ValidarMontosCajaAsync(int idAperturaCaja);
    }
}
