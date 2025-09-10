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

        public async Task<bool> AddUpdateAsync(Traslado traslado)
        {
            var strategy = _farmaDbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();

                try
                {
                    if (traslado.IdTraslado > 0)
                    {
                        // Buscar el traslado existente
                        var existingTraslado = await _farmaDbContext.Traslado.FindAsync(traslado.IdTraslado);

                        if (existingTraslado != null)
                        {
                            // Actualizar propiedades
                            existingTraslado.IdSucursalOrigen = traslado.IdSucursalOrigen;
                            existingTraslado.IdSucursalDestino = traslado.IdSucursalDestino;
                            existingTraslado.FechaTraslado = traslado.FechaTraslado;
                            existingTraslado.IdEstadoTraslado = traslado.IdEstadoTraslado;
                            existingTraslado.Observaciones = traslado.Observaciones;

                            _farmaDbContext.Traslado.Update(existingTraslado);
                            Console.WriteLine($"Traslado actualizado - ID: {traslado.IdTraslado}");
                        }
                        else
                        {
                            Console.WriteLine($"No se encontró traslado con ID: {traslado.IdTraslado}");
                            await transaction.RollbackAsync();
                            return false;
                        }
                    }
                    else
                    {
                        // Crear nuevo traslado
                        traslado.FechaTraslado = traslado.FechaTraslado ?? DateTime.Now;
                        _farmaDbContext.Traslado.Add(traslado);
                        Console.WriteLine("Nuevo traslado creado");
                    }

                    await _farmaDbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Error en AddUpdateAsync: {ex.Message}");
                    Console.WriteLine($"StackTrace: {ex.StackTrace}");
                    return false;
                }
            });
        }

        public async Task<List<Traslado>> GetAllAsync()
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
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
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .FirstOrDefaultAsync(t => t.IdTraslado == id_traslado);

                if (result == null)
                {
                    Console.WriteLine($"No se encontró el traslado con ID {id_traslado}");
                    throw new KeyNotFoundException($"No se encontró el traslado con ID {id_traslado}");
                }

                Console.WriteLine($"Traslado encontrado - ID: {result.IdTraslado}, Origen: {result.IdSucursalOrigen}, Destino: {result.IdSucursalDestino}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByIdAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<MPaginatedResult<Traslado>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            try
            {
                var query = _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .AsQueryable();

                // Aplicar filtros de búsqueda
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(t =>
                        t.IdSucursalOrigenNavigation.NombreSucursal.Contains(searchTerm) ||
                        t.IdSucursalDestinoNavigation.NombreSucursal.Contains(searchTerm) ||
                        t.IdEstadoTrasladoNavigation.Estado1.Contains(searchTerm) ||
                        (t.Observaciones != null && t.Observaciones.Contains(searchTerm)));
                }

                // Aplicar ordenamiento
                query = sortAscending
                    ? query.OrderBy(t => t.FechaTraslado)
                    : query.OrderByDescending(t => t.FechaTraslado);

                var totalCount = await query.CountAsync();
                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new MPaginatedResult<Traslado>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetPaginatedAsync: {ex.Message}");
                return new MPaginatedResult<Traslado>
                {
                    Items = new List<Traslado>(),
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
        }

        public async Task<List<Traslado>> GetBySucursalOrigenAsync(int sucursalOrigenId)
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
                Console.WriteLine($"Error en GetBySucursalOrigenAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<List<Traslado>> GetBySucursalDestinoAsync(int sucursalDestinoId)
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
                Console.WriteLine($"Error en GetBySucursalDestinoAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<List<Traslado>> GetByEstadoAsync(int estadoId)
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
                Console.WriteLine($"Error en GetByEstadoAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<List<Traslado>> GetByDateRangeAsync(DateTime fechaInicio, DateTime fechaFin)
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
                Console.WriteLine($"Error en GetByDateRangeAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<bool> AddDetalleAsync(TrasladoDetalle detalle)
        {
            try
            {
                _farmaDbContext.TrasladoDetalle.Add(detalle);
                await _farmaDbContext.SaveChangesAsync();
                Console.WriteLine($"Detalle agregado - Producto ID: {detalle.IdProducto}, Cantidad: {detalle.Cantidad}, Traslado ID: {detalle.IdTraslado}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en AddDetalleAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateDetalleAsync(TrasladoDetalle detalle)
        {
            try
            {
                var existingDetalle = await _farmaDbContext.TrasladoDetalle
                    .FindAsync(detalle.IdTrasladoDetalle);

                if (existingDetalle != null)
                {
                    existingDetalle.IdProducto = detalle.IdProducto;
                    existingDetalle.Cantidad = detalle.Cantidad;
                    existingDetalle.IdEstado = detalle.IdEstado;

                    _farmaDbContext.TrasladoDetalle.Update(existingDetalle);
                    await _farmaDbContext.SaveChangesAsync();
                    Console.WriteLine($"Detalle actualizado - ID: {detalle.IdTrasladoDetalle}");
                    return true;
                }
                Console.WriteLine($"No se encontró detalle con ID: {detalle.IdTrasladoDetalle}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en UpdateDetalleAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteDetalleAsync(int id_trasladoDetalle)
        {
            try
            {
                var detalle = await _farmaDbContext.TrasladoDetalle
                    .FindAsync(id_trasladoDetalle);

                if (detalle != null)
                {
                    _farmaDbContext.TrasladoDetalle.Remove(detalle);
                    await _farmaDbContext.SaveChangesAsync();
                    Console.WriteLine($"Detalle eliminado - ID: {id_trasladoDetalle}");
                    return true;
                }
                Console.WriteLine($"No se encontró detalle para eliminar con ID: {id_trasladoDetalle}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteDetalleAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<TrasladoDetalle>> GetDetallesByTrasladoIdAsync(int trasladoId)
        {
            try
            {
                Console.WriteLine($"Buscando detalles para traslado ID: {trasladoId}");

                var detalles = await _farmaDbContext.TrasladoDetalle
                    .Include(td => td.IdProductoNavigation)
                    .Include(td => td.IdEstadoNavigation)
                    .Where(td => td.IdTraslado == trasladoId)
                    .ToListAsync();

                Console.WriteLine($"GetDetallesByTrasladoIdAsync - Traslado ID: {trasladoId}, Detalles encontrados: {detalles.Count}");

                // Log adicional para debugging
                if (detalles.Any())
                {
                    foreach (var detalle in detalles)
                    {
                        Console.WriteLine($"  - Detalle ID: {detalle.IdTrasladoDetalle}, Producto: {detalle.IdProducto} ({detalle.IdProductoNavigation?.NombreProducto}), Cantidad: {detalle.Cantidad}");
                    }
                }

                return detalles ?? new List<TrasladoDetalle>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetDetallesByTrasladoIdAsync para traslado {trasladoId}: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                // Retornar lista vacía en caso de error en lugar de lanzar excepción
                return new List<TrasladoDetalle>();
            }
        }

        public async Task<bool> CreateTrasladoWithDetallesAsync(Traslado traslado, List<TrasladoDetalle> detalles)
        {
            var strategy = _farmaDbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();

                try
                {
                    Console.WriteLine($"Iniciando CreateTrasladoWithDetallesAsync - Detalles a agregar: {detalles?.Count ?? 0}");

                    // Crear el traslado
                    traslado.FechaTraslado = traslado.FechaTraslado ?? DateTime.Now;
                    _farmaDbContext.Traslado.Add(traslado);
                    await _farmaDbContext.SaveChangesAsync();

                    Console.WriteLine($"Traslado creado con ID: {traslado.IdTraslado}");

                    // Agregar los detalles solo si hay detalles para agregar
                    if (detalles?.Any() == true)
                    {
                        foreach (var detalle in detalles)
                        {
                            detalle.IdTraslado = traslado.IdTraslado;
                            _farmaDbContext.TrasladoDetalle.Add(detalle);
                            Console.WriteLine($"Preparando detalle - Producto ID: {detalle.IdProducto}, Cantidad: {detalle.Cantidad}");
                        }

                        await _farmaDbContext.SaveChangesAsync();
                        Console.WriteLine($"Se agregaron {detalles.Count} detalles al traslado");
                    }
                    else
                    {
                        Console.WriteLine("No hay detalles para agregar al traslado");
                    }

                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Error en CreateTrasladoWithDetallesAsync: {ex.Message}");
                    Console.WriteLine($"StackTrace: {ex.StackTrace}");
                    return false;
                }
            });
        }

        public async Task<bool> UpdateEstadoTrasladoAsync(int trasladoId, int nuevoEstadoId)
        {
            try
            {
                var traslado = await _farmaDbContext.Traslado.FindAsync(trasladoId);
                if (traslado != null)
                {
                    var estadoAnterior = traslado.IdEstadoTraslado;
                    traslado.IdEstadoTraslado = nuevoEstadoId;
                    _farmaDbContext.Traslado.Update(traslado);
                    await _farmaDbContext.SaveChangesAsync();
                    Console.WriteLine($"Estado actualizado - Traslado ID: {trasladoId}, Estado anterior: {estadoAnterior}, Nuevo estado: {nuevoEstadoId}");
                    return true;
                }
                Console.WriteLine($"No se encontró traslado con ID: {trasladoId}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en UpdateEstadoTrasladoAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ProcessTrasladoAsync(int trasladoId)
        {
            var strategy = _farmaDbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();

                try
                {
                    Console.WriteLine($"Procesando traslado ID: {trasladoId}");

                    var traslado = await _farmaDbContext.Traslado
                        .Include(t => t.IdSucursalOrigenNavigation)
                        .Include(t => t.IdSucursalDestinoNavigation)
                        .FirstOrDefaultAsync(t => t.IdTraslado == trasladoId);

                    if (traslado == null)
                    {
                        Console.WriteLine($"No se encontró traslado con ID: {trasladoId}");
                        await transaction.RollbackAsync();
                        return false;
                    }

                    var detalles = await GetDetallesByTrasladoIdAsync(trasladoId);
                    Console.WriteLine($"Procesando {detalles.Count} detalles del traslado");

                    // Procesar cada detalle del traslado
                    foreach (var detalle in detalles)
                    {
                        if (detalle.IdProducto.HasValue && detalle.Cantidad.HasValue)
                        {
                            Console.WriteLine($"Procesando producto ID: {detalle.IdProducto}, Cantidad: {detalle.Cantidad}");

                            // Restar del inventario origen
                            var inventarioOrigen = await _farmaDbContext.InventarioProducto
                                .FirstOrDefaultAsync(ip =>
                                    ip.IdInventario == traslado.IdSucursalOrigenNavigation.IdInventario &&
                                    ip.IdProducto == detalle.IdProducto);

                            if (inventarioOrigen != null && inventarioOrigen.Cantidad >= detalle.Cantidad)
                            {
                                inventarioOrigen.Cantidad -= detalle.Cantidad;
                                _farmaDbContext.InventarioProducto.Update(inventarioOrigen);
                                Console.WriteLine($"Stock reducido en origen - Producto: {detalle.IdProducto}, Nueva cantidad: {inventarioOrigen.Cantidad}");
                            }
                            else
                            {
                                Console.WriteLine($"Stock insuficiente en origen - Producto: {detalle.IdProducto}, Stock disponible: {inventarioOrigen?.Cantidad ?? 0}, Requerido: {detalle.Cantidad}");
                                await transaction.RollbackAsync();
                                return false; // Stock insuficiente
                            }

                            // Agregar al inventario destino
                            var inventarioDestino = await _farmaDbContext.InventarioProducto
                                .FirstOrDefaultAsync(ip =>
                                    ip.IdInventario == traslado.IdSucursalDestinoNavigation.IdInventario &&
                                    ip.IdProducto == detalle.IdProducto);

                            if (inventarioDestino != null)
                            {
                                inventarioDestino.Cantidad += detalle.Cantidad;
                                _farmaDbContext.InventarioProducto.Update(inventarioDestino);
                                Console.WriteLine($"Stock aumentado en destino - Producto: {detalle.IdProducto}, Nueva cantidad: {inventarioDestino.Cantidad}");
                            }
                            else
                            {
                                // Crear nuevo registro en inventario destino
                                var maxId = await _farmaDbContext.InventarioProducto
                                    .MaxAsync(ip => (int?)ip.IdInventarioProducto) ?? 0;

                                var nuevoInventarioProducto = new InventarioProducto
                                {
                                    IdInventarioProducto = maxId + 1,
                                    IdInventario = traslado.IdSucursalDestinoNavigation.IdInventario,
                                    IdProducto = detalle.IdProducto,
                                    Cantidad = detalle.Cantidad
                                };

                                _farmaDbContext.InventarioProducto.Add(nuevoInventarioProducto);
                                Console.WriteLine($"Nuevo producto creado en destino - Producto: {detalle.IdProducto}, Cantidad: {detalle.Cantidad}");
                            }
                        }
                    }

                    // Marcar traslado como completado
                    var estadoCompletado = await _farmaDbContext.Estado
                        .FirstOrDefaultAsync(e => e.Estado1.ToLower() == "completado");

                    if (estadoCompletado != null)
                    {
                        traslado.IdEstadoTraslado = estadoCompletado.IdEstado;
                        _farmaDbContext.Traslado.Update(traslado);
                        Console.WriteLine($"Traslado marcado como completado");
                    }

                    await _farmaDbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    Console.WriteLine($"Traslado procesado exitosamente - ID: {trasladoId}");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"Error en ProcessTrasladoAsync: {ex.Message}");
                    Console.WriteLine($"StackTrace: {ex.StackTrace}");
                    return false;
                }
            });
        }

        public async Task<Dictionary<string, object>> GetTrasladoStatsAsync()
        {
            var stats = new Dictionary<string, object>();

            try
            {
                var totalTraslados = await _farmaDbContext.Traslado.CountAsync();
                var trasladosPendientes = await _farmaDbContext.Traslado
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .CountAsync(t => t.IdEstadoTrasladoNavigation.Estado1.ToLower() == "pendiente");
                var trasladosCompletados = await _farmaDbContext.Traslado
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .CountAsync(t => t.IdEstadoTrasladoNavigation.Estado1.ToLower() == "completado");
                var trasladosEsteMes = await _farmaDbContext.Traslado
                    .CountAsync(t => t.FechaTraslado.HasValue &&
                               t.FechaTraslado.Value.Month == DateTime.Now.Month &&
                               t.FechaTraslado.Value.Year == DateTime.Now.Year);

                stats["TotalTraslados"] = totalTraslados;
                stats["TrasladosPendientes"] = trasladosPendientes;
                stats["TrasladosCompletados"] = trasladosCompletados;
                stats["TrasladosEsteMes"] = trasladosEsteMes;
                stats["PorcentajeCompletados"] = totalTraslados > 0 ? (double)trasladosCompletados / totalTraslados * 100 : 0;

                Console.WriteLine($"Estadísticas calculadas - Total: {totalTraslados}, Pendientes: {trasladosPendientes}, Completados: {trasladosCompletados}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetTrasladoStatsAsync: {ex.Message}");
            }

            return stats;
        }

        public async Task<List<Traslado>> GetTrasladosPendientesAsync()
        {
            try
            {
                return await _farmaDbContext.Traslado
                    .Include(t => t.IdSucursalOrigenNavigation)
                    .Include(t => t.IdSucursalDestinoNavigation)
                    .Include(t => t.IdEstadoTrasladoNavigation)
                    .Where(t => t.IdEstadoTrasladoNavigation.Estado1.ToLower() == "pendiente")
                    .OrderBy(t => t.FechaTraslado)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetTrasladosPendientesAsync: {ex.Message}");
                return new List<Traslado>();
            }
        }

        public async Task<List<TrasladoDetalle>> DebugGetDetallesByTrasladoIdAsync(int trasladoId)
        {
            try
            {
                Console.WriteLine($"=== DEBUG: Buscando detalles para traslado ID: {trasladoId} ===");

                // Primero verificar si existe el traslado
                var trasladoExists = await _farmaDbContext.Traslado
                    .AnyAsync(t => t.IdTraslado == trasladoId);
                Console.WriteLine($"¿Existe el traslado? {trasladoExists}");

                // Verificar cuántos detalles hay en total en la tabla
                var totalDetalles = await _farmaDbContext.TrasladoDetalle.CountAsync();
                Console.WriteLine($"Total de detalles en la tabla: {totalDetalles}");

                // Obtener todos los IDs de traslado que tienen detalles
                var trasladosConDetalles = await _farmaDbContext.TrasladoDetalle
                    .Select(td => td.IdTraslado)
                    .Distinct()
                    .ToListAsync();
                Console.WriteLine($"Traslados que tienen detalles: [{string.Join(", ", trasladosConDetalles)}]");

                // Buscar detalles específicos para este traslado SIN includes primero
                var detallesSinInclude = await _farmaDbContext.TrasladoDetalle
                    .Where(td => td.IdTraslado == trasladoId)
                    .ToListAsync();
                Console.WriteLine($"Detalles encontrados SIN include: {detallesSinInclude.Count}");

                if (detallesSinInclude.Any())
                {
                    foreach (var detalle in detallesSinInclude)
                    {
                        Console.WriteLine($"  - ID: {detalle.IdTrasladoDetalle}, Producto: {detalle.IdProducto}, Cantidad: {detalle.Cantidad}, Traslado: {detalle.IdTraslado}");
                    }
                }

                // Ahora buscar CON includes
                var detallesConInclude = await _farmaDbContext.TrasladoDetalle
                    .Include(td => td.IdProductoNavigation)
                    .Include(td => td.IdEstadoNavigation)
                    .Where(td => td.IdTraslado == trasladoId)
                    .ToListAsync();
                Console.WriteLine($"Detalles encontrados CON include: {detallesConInclude.Count}");

                if (detallesConInclude.Any())
                {
                    foreach (var detalle in detallesConInclude)
                    {
                        Console.WriteLine($"  - ID: {detalle.IdTrasladoDetalle}, Producto: {detalle.IdProducto} ({detalle.IdProductoNavigation?.NombreProducto}), Cantidad: {detalle.Cantidad}");
                    }
                }

                Console.WriteLine("=== FIN DEBUG ===");

                return detallesConInclude;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR en DebugGetDetallesByTrasladoIdAsync: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                return new List<TrasladoDetalle>();
            }
        }


        public async Task<List<TrasladoDetalle>> GetDetallesSimpleAsync(int trasladoId)
        {
            try
            {
                Console.WriteLine($"GetDetallesSimpleAsync - Buscando traslado ID: {trasladoId}");

                var query = from td in _farmaDbContext.TrasladoDetalle
                            join p in _farmaDbContext.Producto on td.IdProducto equals p.IdProducto into productos
                            from producto in productos.DefaultIfEmpty()
                            join e in _farmaDbContext.Estado on td.IdEstado equals e.IdEstado into estados
                            from estado in estados.DefaultIfEmpty()
                            where td.IdTraslado == trasladoId
                            select new TrasladoDetalle
                            {
                                IdTrasladoDetalle = td.IdTrasladoDetalle,
                                IdProducto = td.IdProducto,
                                Cantidad = td.Cantidad,
                                IdEstado = td.IdEstado,
                                IdTraslado = td.IdTraslado,
                                IdProductoNavigation = producto,
                                IdEstadoNavigation = estado
                            };

                var resultado = await query.ToListAsync();
                Console.WriteLine($"GetDetallesSimpleAsync - Encontrados: {resultado.Count} detalles");

                return resultado;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetDetallesSimpleAsync: {ex.Message}");
                return new List<TrasladoDetalle>();
            }
        }
    }
}