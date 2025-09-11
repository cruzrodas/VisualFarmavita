using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.DetalleOrdenResServices
{
    public class SDetalleOrdenResService : IDetalleOrdenResService
    {
        private readonly FarmaDbContext _farmaDbContext;

        public SDetalleOrdenResService(FarmaDbContext farmaDbContext)
        {
            _farmaDbContext = farmaDbContext;
        }

        public async Task<bool> AddUpdateAsync(DetalleOrdenRes detalleOrdenRes)
        {
            try
            {
                if (detalleOrdenRes.IdDetalle > 0)
                {
                    // Actualizar detalle existente
                    var existingDetalle = await _farmaDbContext.DetalleOrdenRes.FindAsync(detalleOrdenRes.IdDetalle);

                    if (existingDetalle != null)
                    {
                        existingDetalle.IdOrden = detalleOrdenRes.IdOrden;
                        existingDetalle.IdProducto = detalleOrdenRes.IdProducto;
                        existingDetalle.CantidadSolicitada = detalleOrdenRes.CantidadSolicitada;
                        existingDetalle.PrecioUnitario = detalleOrdenRes.PrecioUnitario;
                        existingDetalle.Descuento = detalleOrdenRes.Descuento;
                        existingDetalle.Impuesto = detalleOrdenRes.Impuesto;
                        
                        // Calcular subtotal
                        CalcularSubtotal(existingDetalle);

                        _farmaDbContext.DetalleOrdenRes.Update(existingDetalle);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    // Crear nuevo detalle
                    CalcularSubtotal(detalleOrdenRes);
                    _farmaDbContext.DetalleOrdenRes.Add(detalleOrdenRes);
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

        public async Task<bool> DeleteAsync(int idDetalle)
        {
            try
            {
                var detalle = await _farmaDbContext.DetalleOrdenRes.FindAsync(idDetalle);

                if (detalle != null)
                {
                    _farmaDbContext.DetalleOrdenRes.Remove(detalle);
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

        public async Task<List<DetalleOrdenRes>> GetAllAsync()
        {
            return await _farmaDbContext.DetalleOrdenRes
                .Include(d => d.IdProductoNavigation)
                .Include(d => d.IdOrdenNavigation)
                .ToListAsync();
        }

        public async Task<DetalleOrdenRes> GetByIdAsync(int idDetalle)
        {
            var result = await _farmaDbContext.DetalleOrdenRes
                .Include(d => d.IdProductoNavigation)
                .Include(d => d.IdOrdenNavigation)
                .FirstOrDefaultAsync(d => d.IdDetalle == idDetalle);

            if (result == null)
            {
                throw new KeyNotFoundException($"No se encontró el detalle con ID {idDetalle}");
            }

            return result;
        }

        public async Task<List<DetalleOrdenRes>> GetByOrdenIdAsync(int idOrden)
        {
            return await _farmaDbContext.DetalleOrdenRes
                .Include(d => d.IdProductoNavigation)
                    .ThenInclude(p => p.IdCategoriaNavigation)
                .Where(d => d.IdOrden == idOrden)
                .OrderBy(d => d.IdProductoNavigation.NombreProducto)
                .ToListAsync();
        }

        public async Task<bool> AddDetallesAsync(List<DetalleOrdenRes> detalles)
        {
            var strategy = _farmaDbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();

                try
                {
                    foreach (var detalle in detalles)
                    {
                        CalcularSubtotal(detalle);
                        _farmaDbContext.DetalleOrdenRes.Add(detalle);
                    }

                    await _farmaDbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en AddDetallesAsync: {ex.Message}");
                    await transaction.RollbackAsync();
                    return false;
                }
            });
        }

        public async Task<bool> UpdateDetallesAsync(int idOrden, List<DetalleOrdenRes> detalles)
        {
            var strategy = _farmaDbContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();

                try
                {
                    // Eliminar detalles existentes
                    var detallesExistentes = await _farmaDbContext.DetalleOrdenRes
                        .Where(d => d.IdOrden == idOrden)
                        .ToListAsync();

                    if (detallesExistentes.Any())
                    {
                        _farmaDbContext.DetalleOrdenRes.RemoveRange(detallesExistentes);
                    }

                    // Agregar nuevos detalles
                    foreach (var detalle in detalles)
                    {
                        detalle.IdOrden = idOrden;
                        CalcularSubtotal(detalle);
                        _farmaDbContext.DetalleOrdenRes.Add(detalle);
                    }

                    await _farmaDbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en UpdateDetallesAsync: {ex.Message}");
                    await transaction.RollbackAsync();
                    return false;
                }
            });
        }

        public async Task<bool> DeleteByOrdenIdAsync(int idOrden)
        {
            try
            {
                var detalles = await _farmaDbContext.DetalleOrdenRes
                    .Where(d => d.IdOrden == idOrden)
                    .ToListAsync();

                if (detalles.Any())
                {
                    _farmaDbContext.DetalleOrdenRes.RemoveRange(detalles);
                    await _farmaDbContext.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteByOrdenIdAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<decimal> CalcularTotalOrdenAsync(int idOrden)
        {
            try
            {
                var detalles = await _farmaDbContext.DetalleOrdenRes
                    .Where(d => d.IdOrden == idOrden)
                    .ToListAsync();

                decimal total = 0;

                foreach (var detalle in detalles)
                {
                    if (detalle.Subtotal.HasValue)
                    {
                        total += (decimal)detalle.Subtotal.Value;
                    }
                }

                return total;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en CalcularTotalOrdenAsync: {ex.Message}");
                return 0;
            }
        }

        private void CalcularSubtotal(DetalleOrdenRes detalle)
        {
            if (detalle.CantidadSolicitada.HasValue && detalle.PrecioUnitario.HasValue)
            {
                var subtotalBase = detalle.CantidadSolicitada.Value * detalle.PrecioUnitario.Value;
                var descuento = detalle.Descuento ?? 0;
                var impuesto = detalle.Impuesto ?? 0;

                // Aplicar descuento
                var subtotalConDescuento = subtotalBase - (subtotalBase * (double)descuento / 100.0);

                // Aplicar impuesto
                detalle.Subtotal = subtotalConDescuento + (subtotalConDescuento * (double)impuesto / 100.0);
            }
            else
            {
                detalle.Subtotal = 0;
            }
        }
    }
}