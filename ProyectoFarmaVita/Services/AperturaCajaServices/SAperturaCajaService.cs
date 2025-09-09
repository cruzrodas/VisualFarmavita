using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.AperturaCajaServices
{
    public class SAperturaCajaService : IAperturaCajaService
    {
        private readonly FarmaDbContext _farmaDbContext;

        public SAperturaCajaService(FarmaDbContext farmaDbContext)
        {
            _farmaDbContext = farmaDbContext;
        }

        public async Task<bool> AddUpdateAsync(AperturaCaja aperturaCaja)
        {
            if (aperturaCaja.IdAperturaCaja > 0)
            {
                var existing = await _farmaDbContext.AperturaCaja.FindAsync(aperturaCaja.IdAperturaCaja);
                if (existing == null) return false;

                existing.IdCaja = aperturaCaja.IdCaja;
                existing.IdPersona = aperturaCaja.IdPersona;
                existing.FechaApertura = aperturaCaja.FechaApertura;
                existing.MontoApertura = aperturaCaja.MontoApertura;
                existing.FechaCierre = aperturaCaja.FechaCierre;
                existing.MontoCierre = aperturaCaja.MontoCierre;
                existing.Activa = aperturaCaja.Activa;
                existing.Observaciones = aperturaCaja.Observaciones;

                _farmaDbContext.AperturaCaja.Update(existing);
            }
            else
            {
                _farmaDbContext.AperturaCaja.Add(aperturaCaja);
            }

            await _farmaDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int idAperturaCaja)
        {
            var apertura = await _farmaDbContext.AperturaCaja.Include(a => a.Factura)
                .FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);
            if (apertura == null) return false;

            if (apertura.Factura?.Any() == true)
                throw new InvalidOperationException("No se puede eliminar la apertura de caja porque tiene facturas.");

            if (apertura.Activa == true)
                throw new InvalidOperationException("No se puede eliminar una apertura de caja activa.");

            _farmaDbContext.AperturaCaja.Remove(apertura);
            await _farmaDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<AperturaCaja>> GetAllAsync()
        {
            return await _farmaDbContext.AperturaCaja
                .Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                .Include(a => a.IdPersonaNavigation)
                .OrderByDescending(a => a.FechaApertura)
                .ToListAsync();
        }

        public async Task<AperturaCaja> GetByIdAsync(int idAperturaCaja)
        {
            var result = await _farmaDbContext.AperturaCaja
                .Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                .Include(a => a.IdPersonaNavigation)
                .Include(a => a.Factura)
                .FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);

            if (result == null)
                throw new KeyNotFoundException($"No se encontró la apertura de caja con ID {idAperturaCaja}");

            return result;
        }

        public async Task<MPaginatedResult<AperturaCaja>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            var query = _farmaDbContext.AperturaCaja
                .Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                .Include(a => a.IdPersonaNavigation)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(a =>
                    (a.IdCajaNavigation != null && a.IdCajaNavigation.NombreCaja.Contains(searchTerm)) ||
                    (a.IdPersonaNavigation != null &&
                     (a.IdPersonaNavigation.Nombre.Contains(searchTerm) ||
                      a.IdPersonaNavigation.Apellido.Contains(searchTerm))) ||
                    (a.Observaciones != null && a.Observaciones.Contains(searchTerm)));
            }

            query = sortAscending
                ? query.OrderBy(a => a.FechaApertura).ThenBy(a => a.IdAperturaCaja)
                : query.OrderByDescending(a => a.FechaApertura).ThenByDescending(a => a.IdAperturaCaja);

            var totalItems = await query.CountAsync();

            var items = await query.Skip((pageNumber - 1) * pageSize)
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

        // Métodos para abrir/cerrar caja
        public async Task<int?> AbrirCajaAsync(int idCaja, int idPersona, double montoApertura, string observaciones = "")
        {
            await using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();
            try
            {
                var caja = await _farmaDbContext.Caja.FindAsync(idCaja);
                if (caja == null || caja.Activa != true) return null;

                var aperturaExistente = await _farmaDbContext.AperturaCaja.FirstOrDefaultAsync(a => a.IdCaja == idCaja && a.Activa == true);
                if (aperturaExistente != null)
                    throw new InvalidOperationException("La caja ya tiene una apertura activa.");

                var personaTieneCajaAbierta = await TieneCajaAbiertaAsync(idPersona);
                if (personaTieneCajaAbierta)
                    throw new InvalidOperationException("La persona ya tiene una caja abierta.");

                var nuevaApertura = new AperturaCaja
                {
                    IdCaja = idCaja,
                    IdPersona = idPersona,
                    FechaApertura = DateTime.Now,
                    MontoApertura = montoApertura,
                    Activa = true,
                    Observaciones = observaciones
                };

                _farmaDbContext.AperturaCaja.Add(nuevaApertura);
                await _farmaDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return nuevaApertura.IdAperturaCaja;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> CerrarCajaAsync(int idAperturaCaja, double montoCierre, string observaciones = "")
        {
            await using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();
            var apertura = await _farmaDbContext.AperturaCaja.FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);
            if (apertura == null) return false;
            if (apertura.Activa != true) throw new InvalidOperationException("La apertura ya está cerrada.");

            apertura.FechaCierre = DateTime.Now;
            apertura.MontoCierre = montoCierre;
            apertura.Activa = false;
            if (!string.IsNullOrEmpty(observaciones))
                apertura.Observaciones = string.IsNullOrEmpty(apertura.Observaciones)
                    ? observaciones
                    : $"{apertura.Observaciones}. Cierre: {observaciones}";

            _farmaDbContext.AperturaCaja.Update(apertura);
            await _farmaDbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        public async Task<AperturaCaja?> GetAperturaCajaActivaAsync(int idCaja)
            => await _farmaDbContext.AperturaCaja.Include(a => a.IdCajaNavigation)
                                                 .Include(a => a.IdPersonaNavigation)
                                                 .FirstOrDefaultAsync(a => a.IdCaja == idCaja && a.Activa == true);

        public async Task<List<AperturaCaja>> GetAperturasCajaActivasAsync()
            => await _farmaDbContext.AperturaCaja.Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                                                 .Include(a => a.IdPersonaNavigation)
                                                 .Where(a => a.Activa == true)
                                                 .OrderBy(a => a.FechaApertura)
                                                 .ToListAsync();

        public async Task<bool> TieneCajaAbiertaAsync(int idPersona)
            => await _farmaDbContext.AperturaCaja.AnyAsync(a => a.IdPersona == idPersona && a.Activa == true);

        public async Task<AperturaCaja?> GetCajaAbiertaPorPersonaAsync(int idPersona)
            => await _farmaDbContext.AperturaCaja.Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                                                 .Include(a => a.IdPersonaNavigation)
                                                 .FirstOrDefaultAsync(a => a.IdPersona == idPersona && a.Activa == true);

        // Métodos de consulta
        public async Task<List<AperturaCaja>> GetByPersonaAsync(int idPersona)
            => await _farmaDbContext.AperturaCaja.Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                                                 .Include(a => a.IdPersonaNavigation)
                                                 .Where(a => a.IdPersona == idPersona)
                                                 .OrderByDescending(a => a.FechaApertura)
                                                 .ToListAsync();

        public async Task<List<AperturaCaja>> GetByCajaAsync(int idCaja)
            => await _farmaDbContext.AperturaCaja.Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                                                 .Include(a => a.IdPersonaNavigation)
                                                 .Where(a => a.IdCaja == idCaja)
                                                 .OrderByDescending(a => a.FechaApertura)
                                                 .ToListAsync();

        public async Task<List<AperturaCaja>> GetByFechaAsync(DateTime fecha)
        {
            var inicio = fecha.Date;
            var fin = inicio.AddDays(1);
            return await _farmaDbContext.AperturaCaja.Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                                                     .Include(a => a.IdPersonaNavigation)
                                                     .Where(a => a.FechaApertura >= inicio && a.FechaApertura < fin)
                                                     .OrderByDescending(a => a.FechaApertura)
                                                     .ToListAsync();
        }

        public async Task<List<AperturaCaja>> GetByRangoFechasAsync(DateTime fechaInicio, DateTime fechaFin)
            => await _farmaDbContext.AperturaCaja.Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                                                 .Include(a => a.IdPersonaNavigation)
                                                 .Where(a => a.FechaApertura >= fechaInicio && a.FechaApertura <= fechaFin)
                                                 .OrderByDescending(a => a.FechaApertura)
                                                 .ToListAsync();

        public async Task<Dictionary<string, object>> GetEstadisticasCajaAsync(int idCaja, DateTime? fechaInicio = null, DateTime? fechaFin = null)
        {
            var query = _farmaDbContext.AperturaCaja.Include(a => a.Factura).Where(a => a.IdCaja == idCaja);
            if (fechaInicio.HasValue) query = query.Where(a => a.FechaApertura >= fechaInicio.Value);
            if (fechaFin.HasValue) query = query.Where(a => a.FechaApertura <= fechaFin.Value);

            var aperturas = await query.ToListAsync();
            return new Dictionary<string, object>
            {
                ["TotalAperturas"] = aperturas.Count,
                ["AperturasActivas"] = aperturas.Count(a => a.Activa == true),
                ["MontoTotalApertura"] = aperturas.Sum(a => a.MontoApertura ?? 0),
                ["MontoTotalCierre"] = aperturas.Where(a => a.MontoCierre.HasValue).Sum(a => a.MontoCierre ?? 0),
                ["TotalFacturas"] = aperturas.SelectMany(a => a.Factura ?? new List<Factura>()).Count(),
                ["MontoTotalVentas"] = aperturas.SelectMany(a => a.Factura ?? new List<Factura>()).Sum(f => f.Total ?? 0),
                ["PromedioVentasPorApertura"] = aperturas.Count > 0
                    ? aperturas.SelectMany(a => a.Factura ?? new List<Factura>()).Sum(f => f.Total ?? 0) / aperturas.Count
                    : 0
            };
        }

        public async Task<List<dynamic>> GetResumenCajasAsync(DateTime? fecha = null)
        {
            var f = fecha ?? DateTime.Today;
            var inicio = f.Date;
            var fin = inicio.AddDays(1);

            return await _farmaDbContext.AperturaCaja.Include(a => a.IdCajaNavigation).ThenInclude(c => c.IdSucursalNavigation)
                                                     .Include(a => a.IdPersonaNavigation)
                                                     .Where(a => a.FechaApertura >= inicio && a.FechaApertura < fin)
                                                     .Select(a => new
                                                     {
                                                         a.IdCaja,
                                                         NombreCaja = a.IdCajaNavigation.NombreCaja,
                                                         Sucursal = a.IdCajaNavigation.IdSucursalNavigation.NombreSucursal,
                                                         Responsable = $"{a.IdPersonaNavigation.Nombre} {a.IdPersonaNavigation.Apellido}",
                                                         a.FechaApertura,
                                                         a.FechaCierre,
                                                         a.MontoApertura,
                                                         a.MontoCierre,
                                                         a.Activa,
                                                         Diferencia = (a.MontoCierre ?? 0) - (a.MontoApertura ?? 0)
                                                     })
                                                     .Cast<dynamic>()
                                                     .ToListAsync();
        }

        public async Task<bool> ValidarMontosCajaAsync(int idAperturaCaja)
        {
            var apertura = await _farmaDbContext.AperturaCaja.Include(a => a.Factura)
                                    .FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);
            if (apertura == null) return false;

            var montoEsperado = (apertura.MontoApertura ?? 0) + (apertura.Factura?.Sum(f => f.Total ?? 0) ?? 0);
            return Math.Abs((apertura.MontoCierre ?? 0) - montoEsperado) < 0.01;
        }
    }
}
