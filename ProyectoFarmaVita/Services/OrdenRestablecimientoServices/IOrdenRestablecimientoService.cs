using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.OrdenRestablecimientoServices
{
    public interface IOrdenRestablecimientoService
    {
        Task<bool> AddUpdateAsync(OrdenRestablecimiento ordenRestablecimiento);
        Task<bool> DeleteAsync(int idOrden);
        Task<List<OrdenRestablecimiento>> GetAllAsync();
        Task<OrdenRestablecimiento> GetByIdAsync(int idOrden);
        Task<MPaginatedResult<OrdenRestablecimiento>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true);
        Task<List<OrdenRestablecimiento>> GetByProveedorAsync(int idProveedor);
        Task<List<OrdenRestablecimiento>> GetBySucursalAsync(int idSucursal);
        Task<List<OrdenRestablecimiento>> GetByEstadoAsync(int idEstado);
        Task<bool> ConfirmarOrdenAsync(int idOrden);
        Task<bool> AprobarOrdenAsync(int idOrden, int usuarioAprobacion);
        Task<List<OrdenRestablecimiento>> GetOrdenesPendientesAsync();
        Task<string> GenerarNumeroOrdenAsync();
    }
}