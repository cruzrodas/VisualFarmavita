// IClienteService.cs
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.ClienteServices
{
    public interface IClienteService
    {
        Task<List<Cliente>> GetAllAsync();
        Task<Cliente> GetByIdAsync(int id);
        Task<MPaginatedResult<Cliente>> GetPaginatedAsync(int page, int pageSize, string searchTerm = "");
        Task<bool> AddUpdateAsync(Cliente cliente);
        Task<bool> DeleteAsync(int id);
        Task<List<Cliente>> GetClientesFrecuentesAsync();
        Task<List<Cliente>> SearchClientesAsync(string searchTerm);
        Task<bool> ExisteClienteConNitAsync(string nit, int? excludeId = null);
        Task<bool> ExisteClienteConDpiAsync(long dpi, int? excludeId = null);
    }
}