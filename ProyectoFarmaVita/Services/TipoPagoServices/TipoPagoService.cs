// TipoPagoService.cs
using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;
using ProyectoFarmaVita.Services.TipoPagoServices;

namespace ProyectoFarmaVita.Services.TipoPagoServices
{
    public class TipoPagoService : ITipoPagoService
    {
        private readonly IDbContextFactory<FarmaDbContext> _contextFactory;
        private readonly ILogger<TipoPagoService> _logger;

        public TipoPagoService(IDbContextFactory<FarmaDbContext> contextFactory, ILogger<TipoPagoService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<List<TipoPago>> GetAllAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.TipoPago
                    .OrderBy(tp => tp.NombrePago)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todos los tipos de pago");
                return new List<TipoPago>();
            }
        }

        public async Task<TipoPago> GetByIdAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.TipoPago
                    .FirstOrDefaultAsync(tp => tp.IdTipoPago == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener tipo de pago con ID {id}");
                return null;
            }
        }

        public async Task<MPaginatedResult<TipoPago>> GetPaginatedAsync(int page, int pageSize, string searchTerm = "")
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var query = context.TipoPago.AsQueryable();

                // Aplicar filtro de búsqueda
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(tp => tp.NombrePago.ToLower().Contains(searchTerm));
                }

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderBy(tp => tp.NombrePago)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new MPaginatedResult<TipoPago>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tipos de pago paginados");
                return new MPaginatedResult<TipoPago>
                {
                    Items = new List<TipoPago>(),
                    TotalCount = 0,
                    PageNumber = page,
                    PageSize = pageSize
                };
            }
        }

        public async Task<bool> AddUpdateAsync(TipoPago tipoPago)
        {
            var strategy = _contextFactory.CreateDbContext().Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var context = _contextFactory.CreateDbContext();
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    if (tipoPago.IdTipoPago == 0)
                    {
                        // Nuevo tipo de pago
                        context.TipoPago.Add(tipoPago);
                        _logger.LogInformation($"Creando nuevo tipo de pago: {tipoPago.NombrePago}");
                    }
                    else
                    {
                        // Actualizar tipo de pago existente
                        var tipoPagoExistente = await context.TipoPago
                            .FirstOrDefaultAsync(tp => tp.IdTipoPago == tipoPago.IdTipoPago);

                        if (tipoPagoExistente == null)
                        {
                            _logger.LogWarning($"Tipo de pago con ID {tipoPago.IdTipoPago} no encontrado");
                            return false;
                        }

                        // Actualizar propiedades
                        tipoPagoExistente.NombrePago = tipoPago.NombrePago;

                        context.TipoPago.Update(tipoPagoExistente);
                        _logger.LogInformation($"Actualizando tipo de pago: {tipoPago.NombrePago}");
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error al guardar tipo de pago: {tipoPago.NombrePago}");
                    return false;
                }
            });
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var tipoPago = await context.TipoPago
                    .Include(tp => tp.Factura)
                    .FirstOrDefaultAsync(tp => tp.IdTipoPago == id);

                if (tipoPago == null)
                {
                    _logger.LogWarning($"Tipo de pago con ID {id} no encontrado");
                    return false;
                }

                // Verificar si tiene facturas asociadas
                if (tipoPago.Factura.Any())
                {
                    _logger.LogWarning($"No se puede eliminar el tipo de pago {id} porque tiene facturas asociadas");
                    return false;
                }

                context.TipoPago.Remove(tipoPago);
                await context.SaveChangesAsync();

                _logger.LogInformation($"Tipo de pago {id} eliminado correctamente");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al eliminar tipo de pago {id}");
                return false;
            }
        }

        public async Task<List<TipoPago>> GetTiposPagoActivosAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Como no hay campo "Activo" en TipoPago, devolvemos todos
                return await context.TipoPago
                    .OrderBy(tp => tp.NombrePago)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener tipos de pago activos");
                return new List<TipoPago>();
            }
        }

        public async Task<bool> ExisteTipoPagoConNombreAsync(string nombre, int? excludeId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(nombre))
                    return false;

                using var context = _contextFactory.CreateDbContext();

                var query = context.TipoPago.Where(tp => tp.NombrePago.ToLower() == nombre.ToLower());

                if (excludeId.HasValue)
                {
                    query = query.Where(tp => tp.IdTipoPago != excludeId.Value);
                }

                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al verificar nombre de tipo de pago: {nombre}");
                return false;
            }
        }
    }
}