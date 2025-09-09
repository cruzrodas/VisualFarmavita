using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.TrasladoService
{
    public interface ITrasladoService
    {
        // OPERACIONES BÁSICAS DE TRASLADO
        Task<bool> AddUpdateAsync(Traslado traslado);
        Task<int> AddUpdateAsyncWithId(Traslado traslado); // NUEVO MÉTODO AGREGADO
        Task<bool> DeleteAsync(int id_traslado);
        Task<List<Traslado>> GetAllAsync();
        Task<Traslado> GetByIdAsync(int id_traslado);

        // OPERACIONES DE DETALLE DE TRASLADO
        Task<bool> AddTrasladoDetalleAsync(int trasladoId, int productoId, int cantidad);
        Task<bool> RemoveTrasladoDetalleAsync(int trasladoDetalleId);
        Task<bool> UpdateTrasladoDetalleAsync(int trasladoDetalleId, int nuevaCantidad);
        Task<List<TrasladoDetalle>> GetTrasladoDetallesByTrasladoIdAsync(int trasladoId);

        // OPERACIONES DE ESTADO
        Task<bool> UpdateEstadoTrasladoAsync(int trasladoId, int nuevoEstadoId);
        Task<List<Traslado>> GetTrasladosByEstadoAsync(int estadoId);

        // OPERACIONES DE PROCESAMIENTO
        Task<bool> ProcesarTrasladoAsync(Traslado traslado, List<TrasladoDetalle> detalles);
        Task<bool> ConfirmarRecepcionTrasladoAsync(int trasladoId);
        Task<bool> CancelarTrasladoAsync(int trasladoId, string motivoCancelacion);

        // REPORTES Y ESTADÍSTICAS
        Task<Dictionary<string, object>> GetEstadisticasTrasladosAsync(DateTime? fechaInicio = null, DateTime? fechaFin = null);
    }
}