using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.DetalleOrdenResServices
{
    public class SDetalleOrdenResService : IDetalleOrdenResService
    {
        private readonly IDbContextFactory<FarmaDbContext> _contextFactory;

        public SDetalleOrdenResService(IDbContextFactory<FarmaDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<bool> AddUpdateAsync(DetalleOrdenRes detalleOrdenRes)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                if (detalleOrdenRes.IdDetalle > 0)
                {
                    // Actualizar detalle existente
                    var existingDetalle = await context.DetalleOrdenRes.FindAsync(detalleOrdenRes.IdDetalle);

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

                        context.DetalleOrdenRes.Update(existingDetalle);
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
                    await context.DetalleOrdenRes.AddAsync(detalleOrdenRes);
                }

                await context.SaveChangesAsync();
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
                using var context = _contextFactory.CreateDbContext();
                var detalle = await context.DetalleOrdenRes.FindAsync(idDetalle);

                if (detalle != null)
                {
                    context.DetalleOrdenRes.Remove(detalle);
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

        public async Task<List<DetalleOrdenRes>> GetAllAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.DetalleOrdenRes
                .Include(d => d.IdProductoNavigation)
                .Include(d => d.IdOrdenNavigation)
                .AsNoTracking() // Optimización para consultas de solo lectura
                .ToListAsync();
        }

        public async Task<DetalleOrdenRes> GetByIdAsync(int idDetalle)
        {
            using var context = _contextFactory.CreateDbContext();
            var result = await context.DetalleOrdenRes
                .Include(d => d.IdProductoNavigation)
                .Include(d => d.IdOrdenNavigation)
                .AsNoTracking() // Optimización para consultas de solo lectura
                .FirstOrDefaultAsync(d => d.IdDetalle == idDetalle);

            if (result == null)
            {
                throw new KeyNotFoundException($"No se encontró el detalle con ID {idDetalle}");
            }

            return result;
        }

        public async Task<List<DetalleOrdenRes>> GetByOrdenIdAsync(int idOrden)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.DetalleOrdenRes
                .Include(d => d.IdProductoNavigation)
                    .ThenInclude(p => p.IdCategoriaNavigation)
                .Where(d => d.IdOrden == idOrden)
                .OrderBy(d => d.IdProductoNavigation.NombreProducto)
                .AsNoTracking() // Optimización para consultas de solo lectura
                .ToListAsync();
        }

        public async Task<bool> AddDetallesAsync(List<DetalleOrdenRes> detalles)
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
                        foreach (var detalle in detalles)
                        {
                            CalcularSubtotal(detalle);
                        }

                        await context.DetalleOrdenRes.AddRangeAsync(detalles);
                        await context.SaveChangesAsync();
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error general en AddDetallesAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateDetallesAsync(int idOrden, List<DetalleOrdenRes> detalles)
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
                        // Eliminar detalles existentes
                        var detallesExistentes = await context.DetalleOrdenRes
                            .Where(d => d.IdOrden == idOrden)
                            .ToListAsync();

                        if (detallesExistentes.Any())
                        {
                            context.DetalleOrdenRes.RemoveRange(detallesExistentes);
                        }

                        // Agregar nuevos detalles
                        foreach (var detalle in detalles)
                        {
                            detalle.IdOrden = idOrden;
                            CalcularSubtotal(detalle);
                        }

                        await context.DetalleOrdenRes.AddRangeAsync(detalles);
                        await context.SaveChangesAsync();
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error general en UpdateDetallesAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteByOrdenIdAsync(int idOrden)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var detalles = await context.DetalleOrdenRes
                    .Where(d => d.IdOrden == idOrden)
                    .ToListAsync();

                if (detalles.Any())
                {
                    context.DetalleOrdenRes.RemoveRange(detalles);
                    await context.SaveChangesAsync();
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
                using var context = _contextFactory.CreateDbContext();

                // Optimización: calcular directamente en la base de datos
                var total = await context.DetalleOrdenRes
                    .Where(d => d.IdOrden == idOrden && d.Subtotal.HasValue)
                    .SumAsync(d => (decimal)d.Subtotal.Value);

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