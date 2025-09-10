using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.CajaServices
{
    public class CajaService : ICajaService
    {
        private readonly FarmaDbContext _context;

        public CajaService(FarmaDbContext context)
        {
            _context = context;
        }

        public async Task<bool> AddUpdateAsync(Caja caja)
        {
            try
            {
                if (caja.IdCaja == 0)
                {
                    // Crear nueva caja
                    _context.Caja.Add(caja);
                }
                else
                {
                    // Actualizar caja existente
                    var existingCaja = await _context.Caja.FindAsync(caja.IdCaja);
                    if (existingCaja == null)
                        return false;

                    existingCaja.NombreCaja = caja.NombreCaja;
                    existingCaja.Activa = caja.Activa;
                    existingCaja.IdSucursal = caja.IdSucursal;

                    _context.Caja.Update(existingCaja);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en AddUpdateAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int idCaja)
        {
            try
            {
                var caja = await _context.Caja.FindAsync(idCaja);
                if (caja == null)
                    return false;

                // Soft delete - marcar como inactiva
                caja.Activa = false;
                _context.Caja.Update(caja);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Caja>> GetAllAsync()
        {
            try
            {
                return await _context.Caja
                    .Include(c => c.IdSucursalNavigation)
                    .OrderBy(c => c.NombreCaja)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAllAsync: {ex.Message}");
                return new List<Caja>();
            }
        }

        public async Task<Caja> GetByIdAsync(int idCaja)
        {
            try
            {
                return await _context.Caja
                    .Include(c => c.IdSucursalNavigation)
                    .Include(c => c.AperturaCaja)
                    .FirstOrDefaultAsync(c => c.IdCaja == idCaja);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByIdAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<MPaginatedResult<Caja>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            try
            {
                var query = _context.Caja
                    .Include(c => c.IdSucursalNavigation)
                    .AsQueryable();

                // Aplicar filtro de búsqueda
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(c =>
                        c.NombreCaja.ToLower().Contains(searchTerm) ||
                        c.IdSucursalNavigation.NombreSucursal.ToLower().Contains(searchTerm));
                }

                // Aplicar ordenamiento
                query = sortAscending
                    ? query.OrderBy(c => c.NombreCaja)
                    : query.OrderByDescending(c => c.NombreCaja);

                var totalItems = await query.CountAsync();
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetPaginatedAsync: {ex.Message}");
                return new MPaginatedResult<Caja>
                {
                    Items = new List<Caja>(),
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
        }

        public async Task<List<Caja>> GetActivasAsync()
        {
            try
            {
                return await _context.Caja
                    .Include(c => c.IdSucursalNavigation)
                    .Where(c => c.Activa == true)
                    .OrderBy(c => c.NombreCaja)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetActivasAsync: {ex.Message}");
                return new List<Caja>();
            }
        }

        public async Task<List<Caja>> GetBySucursalAsync(int idSucursal)
        {
            try
            {
                return await _context.Caja
                    .Include(c => c.IdSucursalNavigation)
                    .Where(c => c.IdSucursal == idSucursal && c.Activa == true)
                    .OrderBy(c => c.NombreCaja)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetBySucursalAsync: {ex.Message}");
                return new List<Caja>();
            }
        }

        public async Task<bool> ExistsAsync(string nombreCaja, int? excludeId = null)
        {
            try
            {
                var query = _context.Caja.Where(c => c.NombreCaja.ToLower() == nombreCaja.ToLower());

                if (excludeId.HasValue)
                {
                    query = query.Where(c => c.IdCaja != excludeId.Value);
                }

                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ExistsAsync: {ex.Message}");
                return false;
            }
        }
    }
}
