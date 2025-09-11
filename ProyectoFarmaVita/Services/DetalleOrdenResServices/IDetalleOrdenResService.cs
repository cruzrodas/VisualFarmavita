using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.DetalleOrdenResServices
{
    public interface IDetalleOrdenResService
    {
        Task<bool> AddUpdateAsync(DetalleOrdenRes detalleOrdenRes);
        Task<bool> DeleteAsync(int idDetalle);
        Task<List<DetalleOrdenRes>> GetAllAsync();
        Task<DetalleOrdenRes> GetByIdAsync(int idDetalle);
        Task<List<DetalleOrdenRes>> GetByOrdenIdAsync(int idOrden);
        Task<bool> AddDetallesAsync(List<DetalleOrdenRes> detalles);
        Task<bool> UpdateDetallesAsync(int idOrden, List<DetalleOrdenRes> detalles);
        Task<bool> DeleteByOrdenIdAsync(int idOrden);
        Task<decimal> CalcularTotalOrdenAsync(int idOrden);
    }
}