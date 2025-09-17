using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.OrdenRestablecimientoServices
{
    public class SOrdenRestablecimientoService : IOrdenRestablecimientoService
    {
        private readonly IDbContextFactory<FarmaDbContext> _contextFactory;

        public SOrdenRestablecimientoService(IDbContextFactory<FarmaDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<bool> AddUpdateAsync(OrdenRestablecimiento ordenRestablecimiento)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var strategy = context.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        if (ordenRestablecimiento.IdOrden > 0)
                        {
                            // Actualizar orden existente
                            var existingOrden = await context.OrdenRestablecimiento.FindAsync(ordenRestablecimiento.IdOrden);

                            if (existingOrden != null)
                            {
                                existingOrden.IdProveedor = ordenRestablecimiento.IdProveedor;
                                existingOrden.FechaOrden = ordenRestablecimiento.FechaOrden;
                                existingOrden.FechaRecepcion = ordenRestablecimiento.FechaRecepcion;
                                existingOrden.IdEstado = ordenRestablecimiento.IdEstado;
                                existingOrden.IdPersonaSolicitud = ordenRestablecimiento.IdPersonaSolicitud;
                                existingOrden.IdSucursal = ordenRestablecimiento.IdSucursal;
                                existingOrden.Total = ordenRestablecimiento.Total;
                                existingOrden.Observaciones = ordenRestablecimiento.Observaciones;
                                existingOrden.Aprobada = ordenRestablecimiento.Aprobada;
                                existingOrden.UsuarioAprobacion = ordenRestablecimiento.UsuarioAprobacion;
                                existingOrden.NumeroOrden = ordenRestablecimiento.NumeroOrden;

                                context.OrdenRestablecimiento.Update(existingOrden);
                            }
                            else
                            {
                                await transaction.RollbackAsync();
                                return false;
                            }
                        }
                        else
                        {
                            // Crear nueva orden
                            if (string.IsNullOrEmpty(ordenRestablecimiento.NumeroOrden))
                            {
                                ordenRestablecimiento.NumeroOrden = await GenerarNumeroOrdenAsync();
                            }

                            ordenRestablecimiento.FechaOrden = DateTime.Now;
                            await context.OrdenRestablecimiento.AddAsync(ordenRestablecimiento);
                        }

                        await context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error en AddUpdateAsync: {ex.Message}");
                        await transaction.RollbackAsync();
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error general en AddUpdateAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int idOrden)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var orden = await context.OrdenRestablecimiento
                    .Include(o => o.DetalleOrdenRes)
                    .FirstOrDefaultAsync(o => o.IdOrden == idOrden);

                if (orden != null)
                {
                    // Verificar si la orden ya está confirmada
                    var estadoConfirmado = await context.Estado
                        .FirstOrDefaultAsync(e => e.Estado1.ToLower() == "confirmado");

                    if (orden.IdEstado == estadoConfirmado?.IdEstado)
                    {
                        throw new InvalidOperationException("No se puede eliminar una orden ya confirmada.");
                    }

                    // Eliminar detalles primero
                    if (orden.DetalleOrdenRes?.Any() == true)
                    {
                        context.DetalleOrdenRes.RemoveRange(orden.DetalleOrdenRes);
                    }

                    // Eliminar la orden
                    context.OrdenRestablecimiento.Remove(orden);
                    await context.SaveChangesAsync();
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

        public async Task<List<OrdenRestablecimiento>> GetAllAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Include(o => o.UsuarioAprobacionNavigation)
                .Include(o => o.DetalleOrdenRes)
                    .ThenInclude(d => d.IdProductoNavigation)
                .OrderByDescending(o => o.FechaOrden)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<OrdenRestablecimiento> GetByIdAsync(int idOrden)
        {
            using var context = _contextFactory.CreateDbContext();
            var result = await context.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Include(o => o.UsuarioAprobacionNavigation)
                .Include(o => o.DetalleOrdenRes)
                    .ThenInclude(d => d.IdProductoNavigation)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.IdOrden == idOrden);

            if (result == null)
            {
                throw new KeyNotFoundException($"No se encontró la orden con ID {idOrden}");
            }

            return result;
        }

        public async Task<MPaginatedResult<OrdenRestablecimiento>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            using var context = _contextFactory.CreateDbContext();

            var query = context.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .AsNoTracking()
                .AsQueryable();

            // Aplicar filtros de búsqueda
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o =>
                    o.NumeroOrden.Contains(searchTerm) ||
                    o.IdProveedorNavigation.NombreProveedor.Contains(searchTerm) ||
                    o.IdPersonaSolicitudNavigation.Nombre.Contains(searchTerm) ||
                    o.IdPersonaSolicitudNavigation.Apellido.Contains(searchTerm) ||
                    (o.Observaciones != null && o.Observaciones.Contains(searchTerm)));
            }

            // Aplicar ordenamiento
            query = sortAscending
                ? query.OrderBy(o => o.FechaOrden)
                : query.OrderByDescending(o => o.FechaOrden);

            // Obtener total de registros
            var totalCount = await query.CountAsync();

            // Aplicar paginación
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new MPaginatedResult<OrdenRestablecimiento>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<List<OrdenRestablecimiento>> GetByProveedorAsync(int idProveedor)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.OrdenRestablecimiento
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Where(o => o.IdProveedor == idProveedor)
                .OrderByDescending(o => o.FechaOrden)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<OrdenRestablecimiento>> GetBySucursalAsync(int idSucursal)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Where(o => o.IdSucursal == idSucursal)
                .OrderByDescending(o => o.FechaOrden)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<OrdenRestablecimiento>> GetByEstadoAsync(int idEstado)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Where(o => o.IdEstado == idEstado)
                .OrderByDescending(o => o.FechaOrden)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> ConfirmarOrdenAsync(int idOrden)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var strategy = context.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        var orden = await context.OrdenRestablecimiento
                            .Include(o => o.DetalleOrdenRes)
                            .Include(o => o.IdSucursalNavigation)
                            .FirstOrDefaultAsync(o => o.IdOrden == idOrden);

                        if (orden == null)
                        {
                            Console.WriteLine($"❌ No se encontró la orden con ID {idOrden}");
                            await transaction.RollbackAsync();
                            return false;
                        }

                        // VALIDACIÓN CRÍTICA: Verificar que la orden esté aprobada
                        if (orden.Aprobada != true)
                        {
                            Console.WriteLine($"❌ La orden {orden.NumeroOrden} NO está aprobada. No se puede procesar.");
                            await transaction.RollbackAsync();
                            return false;
                        }

                        Console.WriteLine($"📦 INICIANDO ACTUALIZACIÓN DE INVENTARIOS para orden: {orden.NumeroOrden}");
                        Console.WriteLine($"📍 Sucursal: {orden.IdSucursalNavigation?.NombreSucursal} (Inventario ID: {orden.IdSucursalNavigation?.IdInventario})");

                        // Validar que la sucursal tenga inventario
                        if (orden.IdSucursalNavigation?.IdInventario == null)
                        {
                            Console.WriteLine("❌ La sucursal no tiene inventario configurado");
                            await transaction.RollbackAsync();
                            return false;
                        }

                        // Procesar cada detalle y sumar al inventario
                        foreach (var detalle in orden.DetalleOrdenRes)
                        {
                            if (detalle.IdProducto.HasValue && detalle.CantidadSolicitada.HasValue && detalle.CantidadSolicitada > 0)
                            {
                                var inventario = await context.InventarioProducto
                                    .FirstOrDefaultAsync(ip =>
                                        ip.IdInventario == orden.IdSucursalNavigation.IdInventario &&
                                        ip.IdProducto == detalle.IdProducto);

                                var producto = await context.Producto
                                    .FirstOrDefaultAsync(p => p.IdProducto == detalle.IdProducto);

                                // Convertir cantidad a long para compatibilidad con el modelo
                                var cantidadAgregar = (long)detalle.CantidadSolicitada.Value;

                                if (inventario != null)
                                {
                                    // ACTUALIZAR stock existente
                                    var stockAnterior = inventario.Cantidad ?? 0L;
                                    inventario.Cantidad = stockAnterior + cantidadAgregar;

                                    context.InventarioProducto.Update(inventario);

                                    Console.WriteLine($"  ✅ {producto?.NombreProducto}: {stockAnterior} + {cantidadAgregar} = {inventario.Cantidad}");
                                }
                                else
                                {
                                    // CREAR nuevo registro en inventario
                                    var maxId = await context.InventarioProducto
                                        .MaxAsync(ip => (long?)ip.IdInventarioProducto) ?? 0L;

                                    var nuevoInventarioProducto = new InventarioProducto
                                    {
                                        IdInventarioProducto = (int)(maxId + 1),
                                        IdInventario = orden.IdSucursalNavigation.IdInventario,
                                        IdProducto = detalle.IdProducto,
                                        Cantidad = cantidadAgregar,
                                        StockMinimo = 0L,
                                        StockMaximo = cantidadAgregar * 10L
                                    };

                                    await context.InventarioProducto.AddAsync(nuevoInventarioProducto);
                                    Console.WriteLine($"  🆕 {producto?.NombreProducto}: NUEVO producto en inventario con {cantidadAgregar} unidades");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"  ⚠️ Detalle ignorado: IdProducto={detalle.IdProducto}, CantidadSolicitada={detalle.CantidadSolicitada}");
                            }
                        }

                        // Cambiar estado a completado y establecer fecha de recepción
                        var estadoCompletado = await context.Estado
                            .FirstOrDefaultAsync(e => e.Estado1 != null && e.Estado1.ToLower().Trim() == "completado");

                        if (estadoCompletado != null)
                        {
                            orden.IdEstado = estadoCompletado.IdEstado;
                            orden.FechaRecepcion = DateTime.Now;
                            context.OrdenRestablecimiento.Update(orden);

                            Console.WriteLine($"✅ Orden {orden.NumeroOrden} marcada como completada");
                        }
                        else
                        {
                            Console.WriteLine($"⚠️ No se encontró el estado 'completado' en la base de datos");
                        }

                        // Guardar todos los cambios
                        var changesSaved = await context.SaveChangesAsync();
                        Console.WriteLine($"💾 Cambios guardados: {changesSaved} registros afectados");

                        await transaction.CommitAsync();

                        Console.WriteLine($"🎉 TRANSACCIÓN COMPLETADA - Inventarios actualizados para orden {orden.NumeroOrden}");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error en ConfirmarOrdenAsync (transacción): {ex.Message}");
                        Console.WriteLine($"📋 StackTrace: {ex.StackTrace}");

                        try
                        {
                            await transaction.RollbackAsync();
                        }
                        catch (Exception rollbackEx)
                        {
                            Console.WriteLine($"❌ Error adicional en rollback: {rollbackEx.Message}");
                        }

                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error general en ConfirmarOrdenAsync: {ex.Message}");
                Console.WriteLine($"📋 StackTrace: {ex.StackTrace}");
                return false;
            }
        }

        public async Task<bool> AprobarOrdenAsync(int idOrden, int usuarioAprobacion)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var orden = await context.OrdenRestablecimiento.FindAsync(idOrden);

                if (orden != null)
                {
                    orden.Aprobada = true;
                    orden.UsuarioAprobacion = usuarioAprobacion;

                    context.OrdenRestablecimiento.Update(orden);
                    await context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en AprobarOrdenAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<OrdenRestablecimiento>> GetOrdenesPendientesAsync()
        {
            using var context = _contextFactory.CreateDbContext();

            var estadoPendiente = await context.Estado
                .FirstOrDefaultAsync(e => e.Estado1.ToLower() == "pendiente");

            if (estadoPendiente == null) return new List<OrdenRestablecimiento>();

            return await context.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Where(o => o.IdEstado == estadoPendiente.IdEstado)
                .OrderByDescending(o => o.FechaOrden)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<string> GenerarNumeroOrdenAsync()
        {
            using var context = _contextFactory.CreateDbContext();

            var ultimaOrden = await context.OrdenRestablecimiento
                .OrderByDescending(o => o.IdOrden)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            var numeroSecuencial = (ultimaOrden?.IdOrden ?? 0) + 1;
            return $"ORD-{DateTime.Now:yyyyMM}-{numeroSecuencial:D4}";
        } 


    }
}