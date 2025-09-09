using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.TrasladoService
{
    public class STrasladoService : ITrasladoService
    {
        private readonly FarmaDbContext _farmaDbContext;

        public STrasladoService(FarmaDbContext farmaDbContext)
        {
            _farmaDbContext = farmaDbContext;
        }

        // OPERACIONES BÁSICAS DE TRASLADO
        public async Task<bool> AddUpdateAsync(Traslado traslado)
        {
            try
            {
                if (traslado.IdTraslado == 0)
                {
                    traslado.FechaTraslado = DateTime.Now;
                    traslado.IdEstadoTraslado = 1; // Estado inicial (Pendiente)
                    _farmaDbContext.Traslado.Add(traslado);
                }
                else
                {
                    _farmaDbContext.Traslado.Update(traslado);
                }

                await _farmaDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en AddUpdateAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id_traslado)
        {
            try
            {
                var traslado = await _farmaDbContext.Traslado.FindAsync(id_traslado);
                if (traslado != null)
                {
                    // Verificar que no esté en proceso o completado
                    if (traslado.IdEstadoTraslado != 1) // Solo se puede eliminar si está pendiente
                    {
                        return false;
                    }

                    _farmaDbContext.Traslado.Remove(traslado);
                    await _farmaDbContext.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Traslado>> GetAllAsync()
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .Include(t => t.IdTrasladodetallesNavigation)
                        .ThenInclude(td => td.IdProductoNavigation)
                    .OrderByDescending(t => t.FechaTraslado)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAllAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<Traslado> GetByIdAsync(int id_traslado)
        {
            try
            {
                var result = await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                        .ThenInclude(s => s.IdInventarioNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                        .ThenInclude(s => s.IdInventarioNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .Include(t => t.IdTrasladodetallesNavigation)
                        .ThenInclude(td => td.IdProductoNavigation)
                            .ThenInclude(p => p.IdCategoriaNavigation)
                    .FirstOrDefaultAsync(t => t.IdTraslado == id_traslado);

                if (result == null)
                {
                    throw new KeyNotFoundException($"No se encontró el traslado con ID {id_traslado}");
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al recuperar el traslado", ex);
            }
        }

        public async Task<MPaginatedResult<Traslado>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            var query = _farmaDbContext.Traslado
                .Include(t => t.IdSucursalOrigenNavigation)
                .Include(t => t.IdSucursalDestinoNavigation)
                .Include(t => t.IdEstadoTrasladoNavigation)
                .AsQueryable();

            // Filtro por término de búsqueda
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(t =>
                    t.IdSucursalOrigenNavigation.NombreSucursal.Contains(searchTerm) ||
                    t.IdSucursalDestinoNavigation.NombreSucursal.Contains(searchTerm) ||
                    t.Observaciones.Contains(searchTerm));
            }

            // Ordenamiento
            query = sortAscending
                ? query.OrderBy(t => t.FechaTraslado).ThenBy(t => t.IdTraslado)
                : query.OrderByDescending(t => t.FechaTraslado).ThenByDescending(t => t.IdTraslado);

            var totalItems = await query.CountAsync();

            // Aplicar paginación
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new MPaginatedResult<Traslado>
            {
                Items = items,
                TotalCount = totalItems,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // OPERACIONES DE TRASLADO DETALLE
        public async Task<bool> AddTrasladoDetalleAsync(int trasladoId, int productoId, int cantidad)
        {
            try
            {
                var maxId = await _farmaDbContext.TrasladoDetalle
                    .MaxAsync(td => (int?)td.IdTrasladoDetalle) ?? 0;

                var trasladoDetalle = new TrasladoDetalle
                {
                    IdTrasladoDetalle = maxId + 1,
                    IdProducto = productoId,
                    Cantidad = cantidad,
                    IdEstado = 1 // Estado inicial
                };

                _farmaDbContext.TrasladoDetalle.Add(trasladoDetalle);
                await _farmaDbContext.SaveChangesAsync();

                // Actualizar el traslado para referenciar este detalle
                var traslado = await _farmaDbContext.Traslado.FindAsync(trasladoId);
                if (traslado != null)
                {
                    traslado.IdTrasladodetalles = trasladoDetalle.IdTrasladoDetalle;
                    _farmaDbContext.Traslado.Update(traslado);
                    await _farmaDbContext.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en AddTrasladoDetalleAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveTrasladoDetalleAsync(int trasladoDetalleId)
        {
            try
            {
                var detalle = await _farmaDbContext.TrasladoDetalle.FindAsync(trasladoDetalleId);
                if (detalle != null)
                {
                    _farmaDbContext.TrasladoDetalle.Remove(detalle);
                    await _farmaDbContext.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en RemoveTrasladoDetalleAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateTrasladoDetalleAsync(int trasladoDetalleId, int nuevaCantidad)
        {
            try
            {
                var detalle = await _farmaDbContext.TrasladoDetalle.FindAsync(trasladoDetalleId);
                if (detalle != null)
                {
                    detalle.Cantidad = nuevaCantidad;
                    _farmaDbContext.TrasladoDetalle.Update(detalle);
                    await _farmaDbContext.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en UpdateTrasladoDetalleAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<TrasladoDetalle>> GetTrasladoDetallesByTrasladoIdAsync(int trasladoId)
        {
            try
            {
                var traslado = await _farmaDbContext.Traslado
                    .Include(t => t.IdTrasladodetallesNavigation)
                        .ThenInclude(td => td.IdProductoNavigation)
                            .ThenInclude(p => p.IdCategoriaNavigation)
                    .FirstOrDefaultAsync(t => t.IdTraslado == trasladoId);

                if (traslado?.IdTrasladodetallesNavigation != null)
                {
                    return new List<TrasladoDetalle> { traslado.IdTrasladodetallesNavigation };
                }

                return new List<TrasladoDetalle>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetTrasladoDetallesByTrasladoIdAsync: {ex.Message}");
                return new List<TrasladoDetalle>();
            }
        }

        // OPERACIONES DE ESTADO
        public async Task<bool> UpdateEstadoTrasladoAsync(int trasladoId, int nuevoEstadoId)
        {
            try
            {
                var traslado = await _farmaDbContext.Traslado.FindAsync(trasladoId);
                if (traslado != null)
                {
                    traslado.IdEstadoTraslado = nuevoEstadoId;
                    _farmaDbContext.Traslado.Update(traslado);
                    await _farmaDbContext.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en UpdateEstadoTrasladoAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Traslado>> GetTrasladosByEstadoAsync(int estadoId)
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .Where(t => t.IdEstadoTraslado == estadoId)
                    .OrderByDescending(t => t.FechaTraslado)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetTrasladosByEstadoAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<List<Estado>> GetEstadosTrasladoAsync()
        {
            try
            {
                return await _farmaDbContext.Estado
                    .OrderBy(e => e.IdEstado)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetEstadosTrasladoAsync: {ex.Message}");
                return new List<Estado>();
            }
        }

        // BÚSQUEDAS Y FILTROS
        public async Task<List<Traslado>> GetTrasladosBySucursalOrigenAsync(int sucursalOrigenId)
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .Where(t => t.IdSucursalOrigen == sucursalOrigenId)
                    .OrderByDescending(t => t.FechaTraslado)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetTrasladosBySucursalOrigenAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<List<Traslado>> GetTrasladosBySucursalDestinoAsync(int sucursalDestinoId)
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .Where(t => t.IdSucursalDestino == sucursalDestinoId)
                    .OrderByDescending(t => t.FechaTraslado)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetTrasladosBySucursalDestinoAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<List<Traslado>> GetTrasladosByDateRangeAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .Where(t => t.FechaTraslado >= fechaInicio && t.FechaTraslado <= fechaFin)
                    .OrderByDescending(t => t.FechaTraslado)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetTrasladosByDateRangeAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<List<Traslado>> GetTrasladosPendientesAsync()
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .Where(t => t.IdEstadoTraslado == 1) // Estado pendiente
                    .OrderBy(t => t.FechaTraslado)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetTrasladosPendientesAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        // VALIDACIONES
        public async Task<bool> ValidarDisponibilidadProductoAsync(int sucursalOrigenId, int productoId, int cantidadSolicitada)
        {
            try
            {
                var sucursal = await _farmaDbContext.Sucursal
                    .Include(s => s.IdInventarioNavigation)
                        .ThenInclude(i => i.InventarioProducto)
                    .FirstOrDefaultAsync(s => s.IdSucursal == sucursalOrigenId);

                if (sucursal?.IdInventarioNavigation != null)
                {
                    var inventarioProducto = sucursal.IdInventarioNavigation.InventarioProducto
                        .FirstOrDefault(ip => ip.IdProducto == productoId);

                    return inventarioProducto != null && inventarioProducto.Cantidad >= cantidadSolicitada;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ValidarDisponibilidadProductoAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ValidarTrasladoAsync(Traslado traslado, List<TrasladoDetalle> detalles)
        {
            try
            {
                // Validar que las sucursales existan y sean diferentes
                if (traslado.IdSucursalOrigen == traslado.IdSucursalDestino)
                    return false;

                var sucursalOrigen = await _farmaDbContext.Sucursal.FindAsync(traslado.IdSucursalOrigen);
                var sucursalDestino = await _farmaDbContext.Sucursal.FindAsync(traslado.IdSucursalDestino);

                if (sucursalOrigen == null || sucursalDestino == null)
                    return false;

                // Validar disponibilidad de todos los productos
                foreach (var detalle in detalles)
                {
                    if (!await ValidarDisponibilidadProductoAsync(traslado.IdSucursalOrigen.Value, detalle.IdProducto.Value, detalle.Cantidad.Value))
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ValidarTrasladoAsync: {ex.Message}");
                return false;
            }
        }

        // OPERACIONES AVANZADAS
        public async Task<bool> ProcesarTrasladoCompletoAsync(Traslado traslado, List<TrasladoDetalle> detalles)
        {
            var strategy = _farmaDbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();

                try
                {
                    // 1. Validar el traslado
                    if (!await ValidarTrasladoAsync(traslado, detalles))
                    {
                        return false;
                    }

                    // 2. Crear el traslado
                    traslado.FechaTraslado = DateTime.Now;
                    traslado.IdEstadoTraslado = 2; // En proceso
                    _farmaDbContext.Traslado.Add(traslado);
                    await _farmaDbContext.SaveChangesAsync();

                    // 3. Procesar cada detalle
                    foreach (var detalle in detalles)
                    {
                        // Crear detalle
                        var maxId = await _farmaDbContext.TrasladoDetalle
                            .MaxAsync(td => (int?)td.IdTrasladoDetalle) ?? 0;

                        detalle.IdTrasladoDetalle = maxId + 1;
                        _farmaDbContext.TrasladoDetalle.Add(detalle);
                        await _farmaDbContext.SaveChangesAsync();

                        // Actualizar inventarios
                        await ActualizarInventariosTrasladoAsync(
                            traslado.IdSucursalOrigen.Value,
                            traslado.IdSucursalDestino.Value,
                            detalle.IdProducto.Value,
                            detalle.Cantidad.Value);
                    }

                    // 4. Actualizar estado a completado
                    traslado.IdEstadoTraslado = 3; // Completado
                    _farmaDbContext.Traslado.Update(traslado);
                    await _farmaDbContext.SaveChangesAsync();

                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Error en ProcesarTrasladoCompletoAsync: {ex.Message}");
                    return false;
                }
            });
        }

        private async Task ActualizarInventariosTrasladoAsync(int sucursalOrigenId, int sucursalDestinoId, int productoId, int cantidad)
        {
            // Obtener inventarios de ambas sucursales
            var sucursalOrigen = await _farmaDbContext.Sucursal
                .Include(s => s.IdInventarioNavigation)
                .FirstOrDefaultAsync(s => s.IdSucursal == sucursalOrigenId);

            var sucursalDestino = await _farmaDbContext.Sucursal
                .Include(s => s.IdInventarioNavigation)
                .FirstOrDefaultAsync(s => s.IdSucursal == sucursalDestinoId);

            // Reducir del inventario origen
            var inventarioOrigenProducto = await _farmaDbContext.InventarioProducto
                .FirstOrDefaultAsync(ip => ip.IdInventario == sucursalOrigen.IdInventario && ip.IdProducto == productoId);

            if (inventarioOrigenProducto != null)
            {
                inventarioOrigenProducto.Cantidad -= cantidad;
                _farmaDbContext.InventarioProducto.Update(inventarioOrigenProducto);
            }

            // Aumentar en inventario destino
            var inventarioDestinoProducto = await _farmaDbContext.InventarioProducto
                .FirstOrDefaultAsync(ip => ip.IdInventario == sucursalDestino.IdInventario && ip.IdProducto == productoId);

            if (inventarioDestinoProducto != null)
            {
                inventarioDestinoProducto.Cantidad += cantidad;
                _farmaDbContext.InventarioProducto.Update(inventarioDestinoProducto);
            }
            else
            {
                // Crear nuevo registro en inventario destino
                var maxId = await _farmaDbContext.InventarioProducto
                    .MaxAsync(ip => (int?)ip.IdInventarioProducto) ?? 0;

                var nuevoInventarioProducto = new InventarioProducto
                {
                    IdInventarioProducto = maxId + 1,
                    IdInventario = sucursalDestino.IdInventario.Value,
                    IdProducto = productoId,
                    Cantidad = cantidad,
                    StockMinimo = inventarioOrigenProducto?.StockMinimo,
                    StockMaximo = inventarioOrigenProducto?.StockMaximo
                };

                _farmaDbContext.InventarioProducto.Add(nuevoInventarioProducto);
            }

            await _farmaDbContext.SaveChangesAsync();
        }

        public async Task<bool> ConfirmarRecepcionTrasladoAsync(int trasladoId)
        {
            try
            {
                return await UpdateEstadoTrasladoAsync(trasladoId, 3); // Estado completado
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ConfirmarRecepcionTrasladoAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CancelarTrasladoAsync(int trasladoId, string motivoCancelacion)
        {
            try
            {
                var traslado = await _farmaDbContext.Traslado.FindAsync(trasladoId);
                if (traslado != null)
                {
                    traslado.IdEstadoTraslado = 4; // Estado cancelado
                    traslado.Observaciones = $"{traslado.Observaciones}\nCancelado: {motivoCancelacion}";
                    _farmaDbContext.Traslado.Update(traslado);
                    await _farmaDbContext.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en CancelarTrasladoAsync: {ex.Message}");
                return false;
            }
        }

        // REPORTES Y ESTADÍSTICAS
        public async Task<Dictionary<string, object>> GetEstadisticasTrasladosAsync(DateTime? fechaInicio = null, DateTime? fechaFin = null)
        {
            try
            {
                var query = _farmaDbContext.Traslado.AsQueryable();

                if (fechaInicio.HasValue && fechaFin.HasValue)
                {
                    query = query.Where(t => t.FechaTraslado >= fechaInicio && t.FechaTraslado <= fechaFin);
                }

                var estadisticas = new Dictionary<string, object>
                {
                    ["TotalTraslados"] = await query.CountAsync(),
                    ["TrasladosPendientes"] = await query.CountAsync(t => t.IdEstadoTraslado == 1),
                    ["TrasladosEnProceso"] = await query.CountAsync(t => t.IdEstadoTraslado == 2),
                    ["TrasladosCompletados"] = await query.CountAsync(t => t.IdEstadoTraslado == 3),
                    ["TrasladosCancelados"] = await query.CountAsync(t => t.IdEstadoTraslado == 4),
                    ["FechaUltimoTraslado"] = await query.MaxAsync(t => (DateTime?)t.FechaTraslado)
                };

                return estadisticas;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetEstadisticasTrasladosAsync: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }

        public async Task<List<dynamic>> GetReporteTrasladosPorSucursalAsync()
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .GroupBy(t => new { t.IdSucursalOrigen, t.IdSucursalOrigenNavigation.NombreSucursal })
                    .Select(g => new
                    {
                        SucursalId = g.Key.IdSucursalOrigen,
                        NombreSucursal = g.Key.NombreSucursal,
                        TotalTrasladosEnviados = g.Count(),
                        TrasladosPendientes = g.Count(t => t.IdEstadoTraslado == 1),
                        TrasladosCompletados = g.Count(t => t.IdEstadoTraslado == 3)
                    })
                    .Cast<dynamic>()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetReporteTrasladosPorSucursalAsync: {ex.Message}");
                return new List<dynamic>();
            }
        }

        public async Task<List<dynamic>> GetProductosMasTrasladados(int topCount = 10)
        {
            try
            {
                return await _farmaDbContext.TrasladoDetalle
                    .Include(td => td.IdProductoNavigation)
                    .GroupBy(td => new { td.IdProducto, td.IdProductoNavigation.NombreProducto })
                    .Select(g => new
                    {
                        ProductoId = g.Key.IdProducto,
                        NombreProducto = g.Key.NombreProducto,
                        TotalTrasladado = g.Sum(td => td.Cantidad),
                        VecesTrasladado = g.Count()
                    })
                    .OrderByDescending(x => x.TotalTrasladado)
                    .Take(topCount)
                    .Cast<dynamic>()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetProductosMasTrasladados: {ex.Message}");
                return new List<dynamic>();
            }
        }

        // CONSULTAS DE INVENTARIO
        public async Task<List<InventarioProducto>> GetProductosDisponiblesParaTrasladoAsync(int sucursalOrigenId)
        {
            try
            {
                var sucursal = await _farmaDbContext.Sucursal
                    .Include(s => s.IdInventarioNavigation)
                        .ThenInclude(i => i.InventarioProducto)
                            .ThenInclude(ip => ip.IdProductoNavigation)
                                .ThenInclude(p => p.IdCategoriaNavigation)
                    .FirstOrDefaultAsync(s => s.IdSucursal == sucursalOrigenId);

                if (sucursal?.IdInventarioNavigation?.InventarioProducto != null)
                {
                    return sucursal.IdInventarioNavigation.InventarioProducto
                        .Where(ip => ip.Cantidad > 0)
                        .OrderBy(ip => ip.IdProductoNavigation.NombreProducto)
                        .ToList();
                }

                return new List<InventarioProducto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetProductosDisponiblesParaTrasladoAsync: {ex.Message}");
                return new List<InventarioProducto>();
            }
        }

        public async Task<bool> VerificarInventarioSuficienteAsync(int inventarioId, int productoId, int cantidad)
        {
            try
            {
                var inventarioProducto = await _farmaDbContext.InventarioProducto
                    .FirstOrDefaultAsync(ip => ip.IdInventario == inventarioId && ip.IdProducto == productoId);

                return inventarioProducto != null && inventarioProducto.Cantidad >= cantidad;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en VerificarInventarioSuficienteAsync: {ex.Message}");
                return false;
            }
        }
    }
}