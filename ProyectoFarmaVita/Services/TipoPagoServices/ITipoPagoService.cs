
// ITipoPagoService.cs
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.TipoPagoServices
{
    public interface ITipoPagoService
    {
        Task<List<TipoPago>> GetAllAsync();
        Task<TipoPago> GetByIdAsync(int id);
        Task<MPaginatedResult<TipoPago>> GetPaginatedAsync(int page, int pageSize, string searchTerm = "");
        Task<bool> AddUpdateAsync(TipoPago tipoPago);
        Task<bool> DeleteAsync(int id);
        Task<List<TipoPago>> GetTiposPagoActivosAsync();
        Task<bool> ExisteTipoPagoConNombreAsync(string nombre, int? excludeId = null);
    }
}