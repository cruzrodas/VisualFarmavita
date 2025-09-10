using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.EstadoServices
{
    public interface IEstadoService
    {
        Task<bool> AddUpdateAsync(Estado estado);
        Task<bool> DeleteAsync(int id_estado);
        Task<List<Estado>> GetAllAsync();
        Task<Estado> GetByIdAsync(int id_estado);
        Task<MPaginatedResult<Estado>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true);
        Task<Estado> GetByNombreAsync(string nombre);
        Task<List<Estado>> GetEstadosParaTrasladosAsync();
        Task<List<Estado>> GetEstadosParaFacturasAsync();
    }
}