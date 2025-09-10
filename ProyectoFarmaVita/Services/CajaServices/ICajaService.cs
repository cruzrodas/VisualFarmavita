using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.CajaServices
{
    public interface ICajaService
    {
        Task<bool> AddUpdateAsync(Caja caja);
        Task<bool> DeleteAsync(int idCaja);
        Task<List<Caja>> GetAllAsync();
        Task<Caja> GetByIdAsync(int idCaja);
        Task<MPaginatedResult<Caja>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true);
        Task<List<Caja>> GetActivasAsync();
        Task<List<Caja>> GetBySucursalAsync(int idSucursal);
        Task<bool> ExistsAsync(string nombreCaja, int? excludeId = null);
    }
}