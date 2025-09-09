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

        // OPERACIONES BÁSICAS DE TRASLADO - MÉTODO CORREGIDO
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

        // NUEVO MÉTODO QUE RETORNA EL ID DEL TRASLADO CREADO
        public async Task<int> AddUpdateAsyncWithId(Traslado traslado)
        {
            try
            {
                if (traslado.IdTraslado == 0)
                {
                    traslado.FechaTraslado = DateTime.Now;
                    traslado.IdEstadoTraslado = 1; // Estado inicial (Pendiente)
                    _farmaDbContext.Traslado.Add(traslado);
                    await _farmaDbContext.SaveChangesAsync();

                    Console.WriteLine($"✅ Traslado creado con ID: {traslado.IdTraslado}");
                    return traslado.IdTraslado; // Retorna el ID generado automáticamente
                }
                else
                {
                    _farmaDbContext.Traslado.Update(traslado);
                    await _farmaDbContext.SaveChangesAsync();

                    Console.WriteLine($"✅ Traslado actualizado con ID: {traslado.IdTraslado}");
                    return traslado.IdTraslado;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en AddUpdateAsyncWithId: {ex.Message}");
                return 0; // Indica error
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

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByIdAsync: {ex.Message}");
                return null;
            }
        }

        // MÉTODOS DE TRASLADO DETALLE - CORREGIDOS
        public async Task<bool> AddTrasladoDetalleAsync(int trasladoId, int productoId, int cantidad)
        {
            try
            {
                Console.WriteLine($"🔄 Guardando detalle: Traslado={trasladoId}, Producto={productoId}, Cantidad={cantidad}");

                // Verificar que el traslado existe
                var trasladoExiste = await _farmaDbContext.Traslado.AnyAsync(t => t.IdTraslado == trasladoId);
                if (!trasladoExiste)
                {
                    Console.WriteLine($"❌ El traslado {trasladoId} no existe");
                    return false;
                }

                // Generar nuevo ID para el detalle
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

                Console.WriteLine($"✅ TrasladoDetalle creado con ID: {trasladoDetalle.IdTrasladoDetalle}");

                // CORRECCIÓN: Solo actualizar si el traslado no tiene detalles asignados aún
                var traslado = await _farmaDbContext.Traslado.FindAsync(trasladoId);
                if (traslado != null && traslado.IdTrasladodetalles == null)
                {
                    traslado.IdTrasladodetalles = trasladoDetalle.IdTrasladoDetalle;
                    _farmaDbContext.Traslado.Update(traslado);
                    await _farmaDbContext.SaveChangesAsync();
                    Console.WriteLine($"✅ Traslado {trasladoId} actualizado con primer detalle {trasladoDetalle.IdTrasladoDetalle}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en AddTrasladoDetalleAsync: {ex.Message}");
                Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
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
                // CORRECCIÓN: Buscar TODOS los detalles relacionados con el traslado
                // Primero intentamos por la relación directa
                var traslado = await _farmaDbContext.Traslado
                    .Include(t => t.IdTrasladodetallesNavigation)
                        .ThenInclude(td => td.IdProductoNavigation)
                            .ThenInclude(p => p.IdCategoriaNavigation)
                    .FirstOrDefaultAsync(t => t.IdTraslado == trasladoId);

                var detalles = new List<TrasladoDetalle>();

                if (traslado?.IdTrasladodetallesNavigation != null)
                {
                    detalles.Add(traslado.IdTrasladodetallesNavigation);
                }

                // También buscar otros detalles que puedan existir sin estar referenciados
                // (esto es una solución temporal para el diseño actual de BD)
                var otrosDetalles = await _farmaDbContext.TrasladoDetalle
                    .Include(td => td.IdProductoNavigation)
                        .ThenInclude(p => p.IdCategoriaNavigation)
                    .Where(td => !detalles.Select(d => d.IdTrasladoDetalle).Contains(td.IdTrasladoDetalle))
                    .ToListAsync();

                // Filtrar por lógica de negocio si es necesario
                // (esto requeriría cambios en el diseño de BD para ser más robusto)

                detalles.AddRange(otrosDetalles);

                return detalles;
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

        // OPERACIONES DE PROCESAMIENTO
        public async Task<bool> ProcesarTrasladoAsync(Traslado traslado, List<TrasladoDetalle> detalles)
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

                // 4. Finalizar el traslado
                traslado.IdEstadoTraslado = 3; // Completado
                _farmaDbContext.Traslado.Update(traslado);
                await _farmaDbContext.SaveChangesAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error en ProcesarTrasladoAsync: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ValidarTrasladoAsync(Traslado traslado, List<TrasladoDetalle> detalles)
        {
            try
            {
                // Validar que las sucursales existan
                var sucursalOrigen = await _farmaDbContext.Sucursal.FindAsync(traslado.IdSucursalOrigen);
                var sucursalDestino = await _farmaDbContext.Sucursal.FindAsync(traslado.IdSucursalDestino);

                if (sucursalOrigen == null || sucursalDestino == null)
                {
                    Console.WriteLine("Una o ambas sucursales no existen");
                    return false;
                }

                // Validar que haya detalles
                if (detalles == null || !detalles.Any())
                {
                    Console.WriteLine("El traslado debe tener al menos un detalle");
                    return false;
                }

                // Validar inventario suficiente en sucursal origen
                foreach (var detalle in detalles)
                {
                    var inventarioProducto = await _farmaDbContext.InventarioProducto
                        .FirstOrDefaultAsync(ip => ip.IdInventario == sucursalOrigen.IdInventario &&
                                                   ip.IdProducto == detalle.IdProducto);

                    if (inventarioProducto == null || inventarioProducto.Cantidad < detalle.Cantidad)
                    {
                        Console.WriteLine($"Stock insuficiente para producto {detalle.IdProducto}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ValidarTrasladoAsync: {ex.Message}");
                return false;
            }
        }

        private async Task ActualizarInventariosTrasladoAsync(int sucursalOrigenId, int sucursalDestinoId, int productoId, int cantidad)
        {
            try
            {
                // Obtener inventarios de las sucursales
                var sucursalOrigen = await _farmaDbContext.Sucursal
                    .Include(s => s.IdInventarioNavigation)
                    .FirstOrDefaultAsync(s => s.IdSucursal == sucursalOrigenId);

                var sucursalDestino = await _farmaDbContext.Sucursal
                    .Include(s => s.IdInventarioNavigation)
                    .FirstOrDefaultAsync(s => s.IdSucursal == sucursalDestinoId);

                // Actualizar inventario origen (restar)
                var inventarioOrigenProducto = await _farmaDbContext.InventarioProducto
                    .FirstOrDefaultAsync(ip => ip.IdInventario == sucursalOrigen.IdInventario &&
                                               ip.IdProducto == productoId);

                if (inventarioOrigenProducto != null)
                {
                    inventarioOrigenProducto.Cantidad = Math.Max(0, inventarioOrigenProducto.Cantidad.Value - cantidad);
                    _farmaDbContext.InventarioProducto.Update(inventarioOrigenProducto);
                }

                // Actualizar inventario destino (sumar)
                var inventarioDestinoProducto = await _farmaDbContext.InventarioProducto
                    .FirstOrDefaultAsync(ip => ip.IdInventario == sucursalDestino.IdInventario &&
                                               ip.IdProducto == productoId);

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
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ActualizarInventariosTrasladoAsync: {ex.Message}");
                throw;
            }
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

                if (fechaInicio.HasValue)
                {
                    query = query.Where(t => t.FechaTraslado >= fechaInicio.Value);
                }

                if (fechaFin.HasValue)
                {
                    query = query.Where(t => t.FechaTraslado <= fechaFin.Value);
                }

                var estadisticas = new Dictionary<string, object>
                {
                    ["TotalTraslados"] = await query.CountAsync(),
                    ["TrasladosPendientes"] = await query.CountAsync(t => t.IdEstadoTraslado == 1),
                    ["TrasladosEnProceso"] = await query.CountAsync(t => t.IdEstadoTraslado == 2),
                    ["TrasladosCompletados"] = await query.CountAsync(t => t.IdEstadoTraslado == 3),
                    ["TrasladosCancelados"] = await query.CountAsync(t => t.IdEstadoTraslado == 4)
                };

                return estadisticas;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetEstadisticasTrasladosAsync: {ex.Message}");
                return new Dictionary<string, object>();
            }
        }
    }
}