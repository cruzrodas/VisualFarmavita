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
        Task<AperturaCaja> GetAperturaActivaByCajaAsync(int idCaja);
        Task<List<AperturaCaja>> GetByPersonaAsync(int idPersona);
        Task<List<AperturaCaja>> GetByCajaAsync(int idCaja);
        Task<bool> CerrarAperturaAsync(int idAperturaCaja, double montoCierre, string observaciones = null);
        Task<bool> TieneCajaAbiertaAsync(int idCaja);
        Task<List<AperturaCaja>> GetAperturasActivasAsync();
    }
}