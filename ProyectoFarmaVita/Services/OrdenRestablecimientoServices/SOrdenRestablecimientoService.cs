using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.OrdenRestablecimientoServices
{
    public class SOrdenRestablecimientoService : IOrdenRestablecimientoService
    {
        private readonly FarmaDbContext _farmaDbContext;

        public SOrdenRestablecimientoService(FarmaDbContext farmaDbContext)
        {
            _farmaDbContext = farmaDbContext;
        }

        public async Task<bool> AddUpdateAsync(OrdenRestablecimiento ordenRestablecimiento)
        {
            var strategy = _farmaDbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();

                try
                {
                    if (ordenRestablecimiento.IdOrden > 0)
                    {
                        // Actualizar orden existente
                        var existingOrden = await _farmaDbContext.OrdenRestablecimiento.FindAsync(ordenRestablecimiento.IdOrden);

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

                            _farmaDbContext.OrdenRestablecimiento.Update(existingOrden);
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
                        _farmaDbContext.OrdenRestablecimiento.Add(ordenRestablecimiento);
                    }

                    await _farmaDbContext.SaveChangesAsync();
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

        public async Task<bool> DeleteAsync(int idOrden)
        {
            try
            {
                var orden = await _farmaDbContext.OrdenRestablecimiento
                    .Include(o => o.DetalleOrdenRes)
                    .FirstOrDefaultAsync(o => o.IdOrden == idOrden);

                if (orden != null)
                {
                    // Verificar si la orden ya está confirmada
                    var estadoConfirmado = await _farmaDbContext.Estado
                        .FirstOrDefaultAsync(e => e.Estado1.ToLower() == "confirmado");

                    if (orden.IdEstado == estadoConfirmado?.IdEstado)
                    {
                        throw new InvalidOperationException("No se puede eliminar una orden ya confirmada.");
                    }

                    // Eliminar detalles primero
                    if (orden.DetalleOrdenRes?.Any() == true)
                    {
                        _farmaDbContext.DetalleOrdenRes.RemoveRange(orden.DetalleOrdenRes);
                    }

                    // Eliminar la orden
                    _farmaDbContext.OrdenRestablecimiento.Remove(orden);
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

        public async Task<List<OrdenRestablecimiento>> GetAllAsync()
        {
            return await _farmaDbContext.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Include(o => o.UsuarioAprobacionNavigation)
                .Include(o => o.DetalleOrdenRes)
                    .ThenInclude(d => d.IdProductoNavigation)
                .OrderByDescending(o => o.FechaOrden)
                .ToListAsync();
        }

        public async Task<OrdenRestablecimiento> GetByIdAsync(int idOrden)
        {
            var result = await _farmaDbContext.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Include(o => o.UsuarioAprobacionNavigation)
                .Include(o => o.DetalleOrdenRes)
                    .ThenInclude(d => d.IdProductoNavigation)
                .FirstOrDefaultAsync(o => o.IdOrden == idOrden);

            if (result == null)
            {
                throw new KeyNotFoundException($"No se encontró la orden con ID {idOrden}");
            }

            return result;
        }

        public async Task<MPaginatedResult<OrdenRestablecimiento>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            var query = _farmaDbContext.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .AsQueryable();

            // Aplicar filtros de búsqueda
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(o =>
                    o.NumeroOrden.Contains(searchTerm) ||
                    o.IdProveedorNavigation.NombreProveedor.Contains(searchTerm) ||
                    o.IdPersonaSolicitudNavigation.Nombre.Contains(searchTerm) ||
                    o.IdPersonaSolicitudNavigation.Apellido.Contains(searchTerm) ||
                    o.Observaciones.Contains(searchTerm));
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
            return await _farmaDbContext.OrdenRestablecimiento
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Where(o => o.IdProveedor == idProveedor)
                .OrderByDescending(o => o.FechaOrden)
                .ToListAsync();
        }

        public async Task<List<OrdenRestablecimiento>> GetBySucursalAsync(int idSucursal)
        {
            return await _farmaDbContext.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdEstadoNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Where(o => o.IdSucursal == idSucursal)
                .OrderByDescending(o => o.FechaOrden)
                .ToListAsync();
        }

        public async Task<List<OrdenRestablecimiento>> GetByEstadoAsync(int idEstado)
        {
            return await _farmaDbContext.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Where(o => o.IdEstado == idEstado)
                .OrderByDescending(o => o.FechaOrden)
                .ToListAsync();
        }

        public async Task<bool> ConfirmarOrdenAsync(int idOrden)
        {
            var strategy = _farmaDbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();

                try
                {
                    var orden = await _farmaDbContext.OrdenRestablecimiento
                        .Include(o => o.DetalleOrdenRes)
                        .Include(o => o.IdSucursalNavigation)
                        .FirstOrDefaultAsync(o => o.IdOrden == idOrden);

                    if (orden == null)
                    {
                        Console.WriteLine($"No se encontró la orden con ID {idOrden}");
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Procesar cada detalle y sumar al inventario
                    foreach (var detalle in orden.DetalleOrdenRes)
                    {
                        if (detalle.IdProducto.HasValue && detalle.CantidadSolicitada.HasValue)
                        {
                            var inventario = await _farmaDbContext.InventarioProducto
                                .FirstOrDefaultAsync(ip =>
                                    ip.IdInventario == orden.IdSucursalNavigation.IdInventario &&
                                    ip.IdProducto == detalle.IdProducto);

                            if (inventario != null)
                            {
                                inventario.Cantidad += detalle.CantidadSolicitada;
                                _farmaDbContext.InventarioProducto.Update(inventario);
                                Console.WriteLine($"Stock aumentado - Producto: {detalle.IdProducto}, Nueva cantidad: {inventario.Cantidad}");
                            }
                            else
                            {
                                // Crear nuevo registro en inventario
                                var maxId = await _farmaDbContext.InventarioProducto
                                    .MaxAsync(ip => (int?)ip.IdInventarioProducto) ?? 0;

                                var nuevoInventarioProducto = new InventarioProducto
                                {
                                    IdInventarioProducto = maxId + 1,
                                    IdInventario = orden.IdSucursalNavigation.IdInventario,
                                    IdProducto = detalle.IdProducto,
                                    Cantidad = detalle.CantidadSolicitada
                                };

                                _farmaDbContext.InventarioProducto.Add(nuevoInventarioProducto);
                                Console.WriteLine($"Nuevo producto creado en inventario - Producto: {detalle.IdProducto}, Cantidad: {detalle.CantidadSolicitada}");
                            }
                        }
                    }

                    // Cambiar estado a confirmado
                    var estadoConfirmado = await _farmaDbContext.Estado
                        .FirstOrDefaultAsync(e => e.Estado1.ToLower() == "confirmado");

                    if (estadoConfirmado != null)
                    {
                        orden.IdEstado = estadoConfirmado.IdEstado;
                        orden.FechaRecepcion = DateTime.Now;
                        _farmaDbContext.OrdenRestablecimiento.Update(orden);
                    }

                    await _farmaDbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en ConfirmarOrdenAsync: {ex.Message}");
                    await transaction.RollbackAsync();
                    return false;
                }
            });
        }

        public async Task<bool> AprobarOrdenAsync(int idOrden, int usuarioAprobacion)
        {
            try
            {
                var orden = await _farmaDbContext.OrdenRestablecimiento.FindAsync(idOrden);

                if (orden != null)
                {
                    orden.Aprobada = true;
                    orden.UsuarioAprobacion = usuarioAprobacion;

                    _farmaDbContext.OrdenRestablecimiento.Update(orden);
                    await _farmaDbContext.SaveChangesAsync();
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
            var estadoPendiente = await _farmaDbContext.Estado
                .FirstOrDefaultAsync(e => e.Estado1.ToLower() == "pendiente");

            if (estadoPendiente == null) return new List<OrdenRestablecimiento>();

            return await _farmaDbContext.OrdenRestablecimiento
                .Include(o => o.IdProveedorNavigation)
                .Include(o => o.IdPersonaSolicitudNavigation)
                .Include(o => o.IdSucursalNavigation)
                .Where(o => o.IdEstado == estadoPendiente.IdEstado)
                .OrderByDescending(o => o.FechaOrden)
                .ToListAsync();
        }

        public async Task<string> GenerarNumeroOrdenAsync()
        {
            var ultimaOrden = await _farmaDbContext.OrdenRestablecimiento
                .OrderByDescending(o => o.IdOrden)
                .FirstOrDefaultAsync();

            var numeroSecuencial = (ultimaOrden?.IdOrden ?? 0) + 1;
            return $"ORD-{DateTime.Now:yyyyMM}-{numeroSecuencial:D4}";
        }
    }
}