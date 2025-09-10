using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.AperturaCajaServices
{
    public class AperturaCajaService : IAperturaCajaService
    {
        private readonly FarmaDbContext _context;

        public AperturaCajaService(FarmaDbContext context)
        {
            _context = context;
        }

        public async Task<bool> AddUpdateAsync(AperturaCaja aperturaCaja)
        {
            try
            {
                if (aperturaCaja.IdAperturaCaja == 0)
                {
                    // Verificar que no haya otra apertura activa para la misma caja
                    var aperturaExistente = await GetAperturaActivaByCajaAsync(aperturaCaja.IdCaja.Value);
                    if (aperturaExistente != null)
                    {
                        throw new InvalidOperationException("Ya existe una apertura activa para esta caja");
                    }

                    // Crear nueva apertura
                    aperturaCaja.FechaApertura = DateTime.Now;
                    aperturaCaja.Activa = true;
                    _context.AperturaCaja.Add(aperturaCaja);
                }
                else
                {
                    // Actualizar apertura existente
                    var existingApertura = await _context.AperturaCaja.FindAsync(aperturaCaja.IdAperturaCaja);
                    if (existingApertura == null)
                        return false;

                    existingApertura.MontoApertura = aperturaCaja.MontoApertura;
                    existingApertura.Observaciones = aperturaCaja.Observaciones;

                    _context.AperturaCaja.Update(existingApertura);
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

        public async Task<bool> DeleteAsync(int idAperturaCaja)
        {
            try
            {
                var apertura = await _context.AperturaCaja.FindAsync(idAperturaCaja);
                if (apertura == null)
                    return false;

                // Verificar que no tenga facturas asociadas
                var tieneFacturas = await _context.Factura.AnyAsync(f => f.IdAperturaCaja == idAperturaCaja);
                if (tieneFacturas)
                {
                    throw new InvalidOperationException("No se puede eliminar una apertura que tiene facturas asociadas");
                }

                _context.AperturaCaja.Remove(apertura);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<AperturaCaja>> GetAllAsync()
        {
            try
            {
                return await _context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .OrderByDescending(a => a.FechaApertura)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAllAsync: {ex.Message}");
                return new List<AperturaCaja>();
            }
        }

        public async Task<AperturaCaja> GetByIdAsync(int idAperturaCaja)
        {
            try
            {
                return await _context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Include(a => a.Factura)
                    .FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByIdAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<MPaginatedResult<AperturaCaja>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            try
            {
                var query = _context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .AsQueryable();

                // Aplicar filtro de búsqueda
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(a =>
                        a.IdCajaNavigation.NombreCaja.ToLower().Contains(searchTerm) ||
                        a.IdPersonaNavigation.Nombre.ToLower().Contains(searchTerm) ||
                        a.IdPersonaNavigation.Apellido.ToLower().Contains(searchTerm) ||
                        a.IdCajaNavigation.IdSucursalNavigation.NombreSucursal.ToLower().Contains(searchTerm));
                }

                // Aplicar ordenamiento
                query = sortAscending
                    ? query.OrderBy(a => a.FechaApertura)
                    : query.OrderByDescending(a => a.FechaApertura);

                var totalItems = await query.CountAsync();
                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new MPaginatedResult<AperturaCaja>
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
                return new MPaginatedResult<AperturaCaja>
                {
                    Items = new List<AperturaCaja>(),
                    TotalCount = 0,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
        }

        public async Task<AperturaCaja> GetAperturaActivaByCajaAsync(int idCaja)
        {
            try
            {
                return await _context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .FirstOrDefaultAsync(a => a.IdCaja == idCaja && a.Activa == true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAperturaActivaByCajaAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<List<AperturaCaja>> GetByPersonaAsync(int idPersona)
        {
            try
            {
                return await _context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.IdPersona == idPersona)
                    .OrderByDescending(a => a.FechaApertura)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByPersonaAsync: {ex.Message}");
                return new List<AperturaCaja>();
            }
        }

        public async Task<List<AperturaCaja>> GetByCajaAsync(int idCaja)
        {
            try
            {
                return await _context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.IdCaja == idCaja)
                    .OrderByDescending(a => a.FechaApertura)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByCajaAsync: {ex.Message}");
                return new List<AperturaCaja>();
            }
        }

        public async Task<bool> CerrarAperturaAsync(int idAperturaCaja, double montoCierre, string observaciones = null)
        {
            try
            {
                var apertura = await _context.AperturaCaja.FindAsync(idAperturaCaja);
                if (apertura == null || apertura.Activa != true)
                    return false;

                apertura.FechaCierre = DateTime.Now;
                apertura.MontoCierre = montoCierre;
                apertura.Activa = false;

                if (!string.IsNullOrEmpty(observaciones))
                {
                    apertura.Observaciones = string.IsNullOrEmpty(apertura.Observaciones)
                        ? observaciones
                        : $"{apertura.Observaciones} | Cierre: {observaciones}";
                }

                _context.AperturaCaja.Update(apertura);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en CerrarAperturaAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TieneCajaAbiertaAsync(int idCaja)
        {
            try
            {
                return await _context.AperturaCaja.AnyAsync(a => a.IdCaja == idCaja && a.Activa == true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en TieneCajaAbiertaAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<AperturaCaja>> GetAperturasActivasAsync()
        {
            try
            {
                return await _context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.Activa == true)
                    .OrderBy(a => a.IdCajaNavigation.NombreCaja)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAperturasActivasAsync: {ex.Message}");
                return new List<AperturaCaja>();
            }
        }
    }
}