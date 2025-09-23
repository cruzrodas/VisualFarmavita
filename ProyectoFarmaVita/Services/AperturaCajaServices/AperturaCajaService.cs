using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
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

        //public async Task<bool> AddUpdateAsync(AperturaCaja aperturaCaja)
        //{
        //    try
        //    {
        //        if (aperturaCaja.IdAperturaCaja == 0)
        //        {
        //            // Verificar que no haya otra apertura activa para la misma caja
        //            var aperturaExistente = await GetAperturaActivaByCajaAsync(aperturaCaja.IdCaja.Value);
        //            if (aperturaExistente != null)
        //            {
        //                throw new InvalidOperationException("Ya existe una apertura activa para esta caja");
        //            }

        //            // Crear nueva apertura
        //            aperturaCaja.FechaApertura = DateTime.Now;
        //            aperturaCaja.Activa = true;
        //            aperturaCaja.TotalCaja = aperturaCaja.MontoApertura;
        //            _context.AperturaCaja.Add(aperturaCaja);
        //        }
        //        else
        //        {
        //            // Actualizar apertura existente
        //            var existingApertura = await _context.AperturaCaja.FindAsync(aperturaCaja.IdAperturaCaja);
        //            if (existingApertura == null)
        //                return false;

        //            // Solo actualizar TotalCaja si la apertura está activa y no tiene facturas
        //            var tieneFacturas = await _context.Factura.AnyAsync(f => f.IdAperturaCaja == aperturaCaja.IdAperturaCaja);

        //            existingApertura.MontoApertura = aperturaCaja.MontoApertura;
        //            existingApertura.Observaciones = aperturaCaja.Observaciones;

        //            // Solo recalcular TotalCaja si no hay facturas asociadas
        //            if (!tieneFacturas)
        //            {
        //                existingApertura.TotalCaja = aperturaCaja.MontoApertura;
        //            }
        //            // Si hay facturas, mantener el TotalCaja actual

        //            _context.AperturaCaja.Update(existingApertura);
        //        }

        //        await _context.SaveChangesAsync();
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error en AddUpdateAsync: {ex.Message}");
        //        return false;
        //    }
        //}
        public async Task<bool> AddUpdateAsync(AperturaCaja aperturaCaja)
        {
            try
            {
                if (aperturaCaja.IdAperturaCaja == 0)
                {
                    // VALIDACIÓN ROBUSTA: Verificar que no haya otra apertura activa para la misma caja
                    var aperturaActivaExistente = await GetAperturaActivaByCajaAsync(aperturaCaja.IdCaja.Value);
                    if (aperturaActivaExistente != null)
                    {
                        throw new InvalidOperationException($"Ya existe una apertura activa para la caja {aperturaActivaExistente.IdCajaNavigation?.NombreCaja}. " +
                            $"Debe cerrar la apertura actual (ID: {aperturaActivaExistente.IdAperturaCaja}) antes de crear una nueva.");
                    }

                    // VALIDACIÓN ADICIONAL: Verificar que la caja esté activa
                    var caja = await _context.Caja.FindAsync(aperturaCaja.IdCaja.Value);
                    if (caja == null || caja.Activa != true)
                    {
                        throw new InvalidOperationException("No se puede crear una apertura para una caja inactiva o inexistente.");
                    }

                    // Crear nueva apertura
                    aperturaCaja.FechaApertura = DateTime.Now;
                    aperturaCaja.Activa = true;
                    aperturaCaja.TotalCaja = aperturaCaja.MontoApertura;
                    _context.AperturaCaja.Add(aperturaCaja);
                }
                else
                {
                    // Actualizar apertura existente
                    var existingApertura = await _context.AperturaCaja.FindAsync(aperturaCaja.IdAperturaCaja);
                    if (existingApertura == null)
                        return false;

                    // VALIDACIÓN: No permitir cambios en aperturas cerradas (excepto observaciones)
                    if (existingApertura.Activa != true)
                    {
                        throw new InvalidOperationException("No se puede modificar una apertura de caja que ya está cerrada.");
                    }

                    // VALIDACIÓN: Si se está cambiando la caja, verificar que no haya conflictos
                    if (existingApertura.IdCaja != aperturaCaja.IdCaja)
                    {
                        var aperturaActivaEnNuevaCaja = await GetAperturaActivaByCajaAsync(aperturaCaja.IdCaja.Value);
                        if (aperturaActivaEnNuevaCaja != null && aperturaActivaEnNuevaCaja.IdAperturaCaja != aperturaCaja.IdAperturaCaja)
                        {
                            throw new InvalidOperationException($"La caja seleccionada ya tiene una apertura activa (ID: {aperturaActivaEnNuevaCaja.IdAperturaCaja}).");
                        }
                    }

                    // Solo actualizar TotalCaja si la apertura está activa y no tiene facturas
                    var tieneFacturas = await _context.Factura.AnyAsync(f => f.IdAperturaCaja == aperturaCaja.IdAperturaCaja);

                    existingApertura.IdCaja = aperturaCaja.IdCaja; // Permitir cambio de caja si pasa validaciones
                    existingApertura.IdPersona = aperturaCaja.IdPersona;
                    existingApertura.MontoApertura = aperturaCaja.MontoApertura;
                    existingApertura.Observaciones = aperturaCaja.Observaciones;

                    // Solo recalcular TotalCaja si no hay facturas asociadas
                    if (!tieneFacturas)
                    {
                        existingApertura.TotalCaja = aperturaCaja.MontoApertura;
                    }

                    _context.AperturaCaja.Update(existingApertura);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en AddUpdateAsync: {ex.Message}");
                throw; // Re-lanzar la excepción para que el UI pueda manejarla
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

        public async Task<bool> CerrarAperturaAsync(int idAperturaCaja, string observaciones = null)
        {
            try
            {
                var apertura = await _context.AperturaCaja.FindAsync(idAperturaCaja);
                if (apertura == null || apertura.Activa != true)
                    return false;

                apertura.FechaCierre = DateTime.Now;
                // Usar el TotalCaja actual como MontoCierre
                apertura.MontoCierre = apertura.TotalCaja;
                // TotalCaja permanece con su valor actual
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


        public async Task<(bool IsValid, string ErrorMessage)> ValidateAperturaAsync(AperturaCaja aperturaCaja)
        {
            try
            {
                // Validar que la caja existe y está activa
                var caja = await _context.Caja
                    .Include(c => c.IdSucursalNavigation)
                    .FirstOrDefaultAsync(c => c.IdCaja == aperturaCaja.IdCaja);

                if (caja == null)
                {
                    return (false, "La caja seleccionada no existe.");
                }

                if (caja.Activa != true)
                {
                    return (false, $"La caja '{caja.NombreCaja}' no está activa.");
                }

                // Validar que la persona existe y está activa
                var persona = await _context.Persona.FindAsync(aperturaCaja.IdPersona);
                if (persona == null)
                {
                    return (false, "La persona responsable seleccionada no existe.");
                }

                if (persona.Activo != true)
                {
                    return (false, $"La persona '{persona.Nombre} {persona.Apellido}' no está activa.");
                }

                // Para nuevas aperturas, verificar que no haya apertura activa
                if (aperturaCaja.IdAperturaCaja == 0)
                {
                    var aperturaActiva = await GetAperturaActivaByCajaAsync(aperturaCaja.IdCaja.Value);
                    if (aperturaActiva != null)
                    {
                        return (false, $"La caja '{caja.NombreCaja}' ya tiene una apertura activa desde el {aperturaActiva.FechaApertura?.ToString("dd/MM/yyyy HH:mm")}. " +
                            $"Responsable: {aperturaActiva.IdPersonaNavigation?.Nombre} {aperturaActiva.IdPersonaNavigation?.Apellido}");
                    }
                }

                // Validar monto mínimo
                if (aperturaCaja.MontoApertura <= 0)
                {
                    return (false, "El monto de apertura debe ser mayor a cero.");
                }

                // Validar fecha de apertura (no puede ser futura)
                if (aperturaCaja.FechaApertura.HasValue && aperturaCaja.FechaApertura.Value.Date > DateTime.Now.Date)
                {
                    return (false, "La fecha de apertura no puede ser una fecha futura.");
                }

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ValidateAperturaAsync: {ex.Message}");
                return (false, $"Error al validar: {ex.Message}");
            }
        }

        // Método para obtener el resumen de aperturas por caja
        public async Task<Dictionary<int, AperturaStatusInfo>> GetAperturasStatusByCajaAsync()
        {
            try
            {
                var aperturas = await _context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .GroupBy(a => a.IdCaja)
                    .Select(g => new
                    {
                        IdCaja = g.Key,
                        TotalAperturas = g.Count(),
                        AperturaActiva = g.FirstOrDefault(a => a.Activa == true),
                        UltimaApertura = g.OrderByDescending(a => a.FechaApertura).FirstOrDefault()
                    })
                    .ToListAsync();

                var result = new Dictionary<int, AperturaStatusInfo>();

                foreach (var item in aperturas)
                {
                    result[item.IdCaja.Value] = new AperturaStatusInfo
                    {
                        IdCaja = item.IdCaja.Value,
                        TotalAperturas = item.TotalAperturas,
                        TieneAperturaActiva = item.AperturaActiva != null,
                        AperturaActiva = item.AperturaActiva,
                        UltimaApertura = item.UltimaApertura
                    };
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAperturasStatusByCajaAsync: {ex.Message}");
                return new Dictionary<int, AperturaStatusInfo>();
            }
        }

        // Clase auxiliar para el status de aperturas
        public class AperturaStatusInfo
        {
            public int IdCaja { get; set; }
            public int TotalAperturas { get; set; }
            public bool TieneAperturaActiva { get; set; }
            public AperturaCaja AperturaActiva { get; set; }
            public AperturaCaja UltimaApertura { get; set; }
        }

    }
}