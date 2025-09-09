using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.TrasladoService
{
    public interface ITrasladoService
    {
        // OPERACIONES BÁSICAS DE TRASLADO
        Task<bool> AddUpdateAsync(Traslado traslado);
        Task<bool> DeleteAsync(int id_traslado);
        Task<List<Traslado>> GetAllAsync();
        Task<Traslado> GetByIdAsync(int id_traslado);
        Task<MPaginatedResult<Traslado>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true);

        // OPERACIONES DE TRASLADO DETALLE
        Task<bool> AddTrasladoDetalleAsync(int trasladoId, int productoId, int cantidad);
        Task<bool> RemoveTrasladoDetalleAsync(int trasladoDetalleId);
        Task<bool> UpdateTrasladoDetalleAsync(int trasladoDetalleId, int nuevaCantidad);
        Task<List<TrasladoDetalle>> GetTrasladoDetallesByTrasladoIdAsync(int trasladoId);

        // OPERACIONES DE ESTADO
        Task<bool> UpdateEstadoTrasladoAsync(int trasladoId, int nuevoEstadoId);
        Task<List<Traslado>> GetTrasladosByEstadoAsync(int estadoId);
        Task<List<Estado>> GetEstadosTrasladoAsync();

        // BÚSQUEDAS Y FILTROS
        Task<List<Traslado>> GetTrasladosBySucursalOrigenAsync(int sucursalOrigenId);
        Task<List<Traslado>> GetTrasladosBySucursalDestinoAsync(int sucursalDestinoId);
        Task<List<Traslado>> GetTrasladosByDateRangeAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<List<Traslado>> GetTrasladosPendientesAsync();

        // VALIDACIONES
        Task<bool> ValidarDisponibilidadProductoAsync(int sucursalOrigenId, int productoId, int cantidadSolicitada);
        Task<bool> ValidarTrasladoAsync(Traslado traslado, List<TrasladoDetalle> detalles);

        // OPERACIONES AVANZADAS
        Task<bool> ProcesarTrasladoCompletoAsync(Traslado traslado, List<TrasladoDetalle> detalles);
        Task<bool> ConfirmarRecepcionTrasladoAsync(int trasladoId);
        Task<bool> CancelarTrasladoAsync(int trasladoId, string motivoCancelacion);

        // REPORTES Y ESTADÍSTICAS
        Task<Dictionary<string, object>> GetEstadisticasTrasladosAsync(DateTime? fechaInicio = null, DateTime? fechaFin = null);
        Task<List<dynamic>> GetReporteTrasladosPorSucursalAsync();
        Task<List<dynamic>> GetProductosMasTrasladados(int topCount = 10);

        // CONSULTAS DE INVENTARIO
        Task<List<InventarioProducto>> GetProductosDisponiblesParaTrasladoAsync(int sucursalOrigenId);
        Task<bool> VerificarInventarioSuficienteAsync(int inventarioId, int productoId, int cantidad);
    }
}