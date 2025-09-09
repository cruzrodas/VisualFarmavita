using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;
using ProyectoFarmaVita.Services.CajaServices;

namespace ProyectoFarmaVita.Services.CajaServices
{
    public class SCajaService : ICajaService
    {
        private readonly FarmaDbContext _farmaDbContext;

        public SCajaService(FarmaDbContext farmaDbContext)
        {
            _farmaDbContext = farmaDbContext;
        }

        public async Task<bool> AddUpdateAsync(Caja caja)
        {
            if (caja.IdCaja > 0)
            {
                // Buscar la caja existente en la base de datos
                var existingCaja = await _farmaDbContext.Caja.FindAsync(caja.IdCaja);

                if (existingCaja != null)
                {
                    // Si se intenta desactivar, verificar que no tenga aperturas activas
                    if (existingCaja.Activa == true && caja.Activa == false)
                    {
                        var hasActiveCashOpenings = await _farmaDbContext.AperturaCaja
                            .AnyAsync(a => a.IdCaja == caja.IdCaja && a.Activa == true);

                        if (hasActiveCashOpenings)
                        {
                            throw new InvalidOperationException("No se puede desactivar la caja porque tiene aperturas activas. Cierre todas las aperturas antes de desactivar la caja.");
                        }
                    }

                    // Actualizar todas las propiedades necesarias
                    existingCaja.NombreCaja = caja.NombreCaja;
                    existingCaja.IdSucursal = caja.IdSucursal;
                    existingCaja.Activa = caja.Activa; // ESTA LÍNEA FALTABA - FIX PRINCIPAL

                    // Marcar la caja como modificada
                    _farmaDbContext.Caja.Update(existingCaja);
                }
                else
                {
                    return false; // Si no se encontró la caja, devolver false
                }
            }
            else
            {
                caja.Activa = true;

                // Si no hay ID, se trata de una nueva caja, agregarla
                _farmaDbContext.Caja.Add(caja);
            }

            // Guardar los cambios en la base de datos
            await _farmaDbContext.SaveChangesAsync();
            return true; // Retornar true si se ha agregado o actualizado correctamente
        }

        public async Task<bool> DeleteAsync(int idCaja)
        {
            var caja = await _farmaDbContext.Caja
                .Include(c => c.AperturaCaja)
                .FirstOrDefaultAsync(c => c.IdCaja == idCaja);

            if (caja != null)
            {
                // Verificar si la caja tiene aperturas asociadas
                bool hasOpenCash = caja.AperturaCaja?.Any(a => a.Activa == true) == true;

                if (hasOpenCash)
                {
                    throw new InvalidOperationException("No se puede eliminar la caja porque tiene aperturas activas.");
                }

                // Eliminar lógicamente: cambiar estado a inactiva
                caja.Activa = false;

                _farmaDbContext.Caja.Update(caja);
                await _farmaDbContext.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<List<Caja>> GetAllAsync()
        {
            return await _farmaDbContext.Caja
                .Include(c => c.IdSucursalNavigation)
                .Include(c => c.AperturaCaja.Where(a => a.Activa == true))
                .OrderBy(c => c.NombreCaja)
                .ToListAsync();
        }

        public async Task<List<Caja>> GetActivasAsync()
        {
            return await _farmaDbContext.Caja
                .Include(c => c.IdSucursalNavigation)
                .Include(c => c.AperturaCaja.Where(a => a.Activa == true))
                .Where(c => c.Activa == true)
                .OrderBy(c => c.NombreCaja)
                .ToListAsync();
        }

        public async Task<Caja> GetByIdAsync(int idCaja)
        {
            try
            {
                var result = await _farmaDbContext.Caja
                    .Include(c => c.IdSucursalNavigation)
                    .Include(c => c.AperturaCaja)
                        .ThenInclude(a => a.IdPersonaNavigation)
                    .FirstOrDefaultAsync(c => c.IdCaja == idCaja);

                if (result == null)
                {
                    throw new KeyNotFoundException($"No se encontró la caja con ID {idCaja}");
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al recuperar la caja", ex);
            }
        }

        public async Task<MPaginatedResult<Caja>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            var query = _farmaDbContext.Caja
                .Include(c => c.IdSucursalNavigation)
                .Include(c => c.AperturaCaja.Where(a => a.Activa == true))
                .Where(c => c.Activa == true) // Solo cajas activas
                .AsQueryable();

            // Filtro por el término de búsqueda
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(c =>
                    c.NombreCaja.Contains(searchTerm) ||
                    (c.IdSucursalNavigation != null && c.IdSucursalNavigation.NombreSucursal.Contains(searchTerm)));
            }

            // Ordenamiento
            query = sortAscending
                ? query.OrderBy(c => c.NombreCaja).ThenBy(c => c.IdCaja)
                : query.OrderByDescending(c => c.NombreCaja).ThenByDescending(c => c.IdCaja);

            var totalItems = await query.CountAsync();

            // Aplicar paginación
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new MPaginatedResult<Caja>
            {
                Items = items,
                TotalCount = totalItems,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<List<Caja>> GetBySucursalAsync(int idSucursal)
        {
            return await _farmaDbContext.Caja
                .Include(c => c.IdSucursalNavigation)
                .Include(c => c.AperturaCaja.Where(a => a.Activa == true))
                .Where(c => c.Activa == true && c.IdSucursal == idSucursal)
                .OrderBy(c => c.NombreCaja)
                .ToListAsync();
        }

        public async Task<bool> ExistsByNombreAsync(string nombreCaja, int? excludeId = null)
        {
            if (string.IsNullOrEmpty(nombreCaja))
                return false;

            var query = _farmaDbContext.Caja
                .Where(c => c.NombreCaja.ToLower() == nombreCaja.ToLower() && c.Activa == true);

            if (excludeId.HasValue)
            {
                query = query.Where(c => c.IdCaja != excludeId.Value);
            }

            return await query.AnyAsync();
        }

        public async Task<List<Caja>> GetCajasDispobiblesAsync(int? sucursalId = null)
        {
            var query = _farmaDbContext.Caja
                .Include(c => c.IdSucursalNavigation)
                .Include(c => c.AperturaCaja)
                .Where(c => c.Activa == true);

            if (sucursalId.HasValue)
            {
                query = query.Where(c => c.IdSucursal == sucursalId.Value);
            }

            // Cajas que no tienen aperturas activas
            query = query.Where(c => !c.AperturaCaja.Any(a => a.Activa == true));

            return await query
                .OrderBy(c => c.NombreCaja)
                .ToListAsync();
        }

        public async Task<bool> TieneCajasAbiertasAsync(int idCaja)
        {
            return await _farmaDbContext.AperturaCaja
                .AnyAsync(a => a.IdCaja == idCaja && a.Activa == true);
        }
    }
}