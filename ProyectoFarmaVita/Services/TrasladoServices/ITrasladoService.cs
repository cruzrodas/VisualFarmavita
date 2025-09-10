using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.TrasladoService
{
    public interface ITrasladoService
    {
        // Métodos principales del traslado
        Task<bool> AddUpdateAsync(Traslado traslado);
        Task<List<Traslado>> GetAllAsync();
        Task<Traslado> GetByIdAsync(int id_traslado);
        Task<MPaginatedResult<Traslado>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true);

        // Métodos de búsqueda específicos
        Task<List<Traslado>> GetBySucursalOrigenAsync(int sucursalOrigenId);
        Task<List<Traslado>> GetBySucursalDestinoAsync(int sucursalDestinoId);
        Task<List<Traslado>> GetByEstadoAsync(int estadoId);
        Task<List<Traslado>> GetByDateRangeAsync(DateTime fechaInicio, DateTime fechaFin);

        // Métodos para detalles del traslado
        Task<bool> AddDetalleAsync(TrasladoDetalle detalle);
        Task<bool> UpdateDetalleAsync(TrasladoDetalle detalle);
        Task<bool> DeleteDetalleAsync(int id_trasladoDetalle);
        Task<List<TrasladoDetalle>> GetDetallesByTrasladoIdAsync(int trasladoId);

        // Métodos avanzados
        Task<bool> CreateTrasladoWithDetallesAsync(Traslado traslado, List<TrasladoDetalle> detalles);
        Task<bool> UpdateEstadoTrasladoAsync(int trasladoId, int nuevoEstadoId);
        Task<bool> ProcessTrasladoAsync(int trasladoId);

        // Métodos de estadísticas y reportes
        Task<Dictionary<string, object>> GetTrasladoStatsAsync();
        Task<List<Traslado>> GetTrasladosPendientesAsync();

        Task<List<TrasladoDetalle>> DebugGetDetallesByTrasladoIdAsync(int trasladoId);
        Task<List<TrasladoDetalle>> GetDetallesSimpleAsync(int trasladoId);

        Task<bool> UpdateTrasladoWithDetallesAsync(int trasladoId, Traslado traslado, List<TrasladoDetalle> nuevosDetalles);

    }
}