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
            try
            {
                if (aperturaCaja.IdAperturaCaja > 0)
                {
                    var existing = await _farmaDbContext.AperturaCaja.FindAsync(aperturaCaja.IdAperturaCaja);
                    if (existing == null) return false;

                    // Actualizar solo si los valores son diferentes
                    existing.IdCaja = aperturaCaja.IdCaja;
                    existing.IdPersona = aperturaCaja.IdPersona;
                    existing.FechaApertura = aperturaCaja.FechaApertura;
                    existing.MontoApertura = aperturaCaja.MontoApertura;
                    existing.FechaCierre = aperturaCaja.FechaCierre;
                    existing.MontoCierre = aperturaCaja.MontoCierre;
                    existing.Activa = aperturaCaja.Activa;
                    existing.Observaciones = aperturaCaja.Observaciones;

                    _farmaDbContext.Entry(existing).State = EntityState.Modified;
                }
                else
                {
                    _farmaDbContext.AperturaCaja.Add(aperturaCaja);
                }

                await _farmaDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Log el error para debugging
                Console.WriteLine($"Error en AddUpdateAsync: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw new InvalidOperationException($"Error al guardar apertura de caja: {ex.Message}", ex);
            }
        }

        public async Task<List<AperturaCaja>> GetAllAsync()
        {
            try
            {
                var result = await _farmaDbContext.AperturaCaja
                    .AsNoTracking() // Mejorar rendimiento para consultas de solo lectura
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .OrderByDescending(a => a.FechaApertura)
                    .ToListAsync();

                Console.WriteLine($"GetAllAsync: Se encontraron {result.Count} registros");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAllAsync: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw new InvalidOperationException($"Error al obtener todas las aperturas de caja: {ex.Message}", ex);
            }
        }

        public async Task<AperturaCaja> GetByIdAsync(int idAperturaCaja)
        {
            try
            {
                Console.WriteLine($"Buscando apertura con ID: {idAperturaCaja}");

                var result = await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Include(a => a.Factura)
                    .FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);

                if (result == null)
                {
                    Console.WriteLine($"No se encontró apertura con ID: {idAperturaCaja}");
                    throw new KeyNotFoundException($"No se encontró la apertura de caja con ID {idAperturaCaja}");
                }

                Console.WriteLine($"Apertura encontrada: {result.IdAperturaCaja}");
                return result;
            }
            catch (KeyNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByIdAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener apertura de caja por ID: {ex.Message}", ex);
            }
        }

        public async Task<MPaginatedResult<AperturaCaja>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            try
            {
                Console.WriteLine($"GetPaginatedAsync - Página: {pageNumber}, Tamaño: {pageSize}, Búsqueda: '{searchTerm}'");

                // Validar parámetros de entrada
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 1000) pageSize = 1000;

                var query = _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .AsQueryable();

                // Aplicar filtros de búsqueda con validación mejorada
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.Trim().ToLower();
                    Console.WriteLine($"Aplicando filtro de búsqueda: {searchTerm}");

                    query = query.Where(a =>
                        (a.IdCajaNavigation != null &&
                         a.IdCajaNavigation.NombreCaja != null &&
                         EF.Functions.Like(a.IdCajaNavigation.NombreCaja.ToLower(), $"%{searchTerm}%")) ||
                        (a.IdPersonaNavigation != null &&
                         ((a.IdPersonaNavigation.Nombre != null &&
                           EF.Functions.Like(a.IdPersonaNavigation.Nombre.ToLower(), $"%{searchTerm}%")) ||
                          (a.IdPersonaNavigation.Apellido != null &&
                           EF.Functions.Like(a.IdPersonaNavigation.Apellido.ToLower(), $"%{searchTerm}%")))) ||
                        (a.Observaciones != null &&
                         EF.Functions.Like(a.Observaciones.ToLower(), $"%{searchTerm}%")));
                }

                // Aplicar ordenamiento
                query = sortAscending
                    ? query.OrderBy(a => a.FechaApertura).ThenBy(a => a.IdAperturaCaja)
                    : query.OrderByDescending(a => a.FechaApertura).ThenByDescending(a => a.IdAperturaCaja);

                var totalItems = await query.CountAsync();
                Console.WriteLine($"Total de items encontrados: {totalItems}");

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                Console.WriteLine($"Items en la página actual: {items.Count}");

                return new MPaginatedResult<AperturaCaja>
                {
                    Items = items ?? new List<AperturaCaja>(),
                    TotalCount = totalItems,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetPaginatedAsync: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                throw new InvalidOperationException($"Error al obtener datos paginados: {ex.Message}", ex);
            }
        }

        public async Task<int?> AbrirCajaAsync(int idCaja, int idPersona, double montoApertura, string observaciones = "")
        {
            using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();
            try
            {
                Console.WriteLine($"Intentando abrir caja {idCaja} para persona {idPersona}");

                // Validar parámetros
                if (montoApertura < 0)
                    throw new ArgumentException("El monto de apertura no puede ser negativo");

                // Verificar que la caja existe y está activa
                var caja = await _farmaDbContext.Caja
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.IdCaja == idCaja);

                if (caja == null)
                    throw new InvalidOperationException("La caja no existe");

                if (caja.Activa != true)
                    throw new InvalidOperationException("La caja no está activa");

                // Verificar que no existe apertura activa para esta caja
                var aperturaExistente = await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.IdCaja == idCaja && a.Activa == true);

                if (aperturaExistente != null)
                    throw new InvalidOperationException("La caja ya tiene una apertura activa.");

                // Verificar que la persona no tiene otra caja abierta
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
                    Observaciones = observaciones ?? string.Empty
                };

                _farmaDbContext.AperturaCaja.Add(nuevaApertura);
                await _farmaDbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"Caja abierta exitosamente con ID: {nuevaApertura.IdAperturaCaja}");
                return nuevaApertura.IdAperturaCaja;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al abrir caja: {ex.Message}");
                await transaction.RollbackAsync();
                throw;
            }
        }

        // Método para diagnosticar problemas de conexión
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var canConnect = await _farmaDbContext.Database.CanConnectAsync();
                Console.WriteLine($"Conexión a la base de datos: {(canConnect ? "EXITOSA" : "FALLIDA")}");

                if (canConnect)
                {
                    var count = await _farmaDbContext.AperturaCaja.CountAsync();
                    Console.WriteLine($"Total de registros en AperturaCaja: {count}");
                }

                return canConnect;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al probar conexión: {ex.Message}");
                return false;
            }
        }

        // Método para verificar la estructura de la tabla
        public async Task<List<string>> GetTableInfoAsync()
        {
            try
            {
                var info = new List<string>();

                // Verificar si la tabla existe
                var tableExists = await _farmaDbContext.Database
                    .SqlQueryRaw<int>("SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'AperturaCaja'")
                    .FirstOrDefaultAsync();

                info.Add($"Tabla AperturaCaja existe: {tableExists > 0}");

                if (tableExists > 0)
                {
                    var recordCount = await _farmaDbContext.AperturaCaja.CountAsync();
                    info.Add($"Número de registros: {recordCount}");

                    if (recordCount > 0)
                    {
                        var sample = await _farmaDbContext.AperturaCaja
                            .AsNoTracking()
                            .Take(1)
                            .FirstOrDefaultAsync();

                        info.Add($"Registro de ejemplo - ID: {sample?.IdAperturaCaja}, Activa: {sample?.Activa}");
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                return new List<string> { $"Error al obtener información de la tabla: {ex.Message}" };
            }
        }

        // Resto de métodos sin cambios significativos...
        public async Task<bool> DeleteAsync(int idAperturaCaja)
        {
            try
            {
                var apertura = await _farmaDbContext.AperturaCaja
                    .Include(a => a.Factura)
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al eliminar apertura de caja: {ex.Message}", ex);
            }
        }

        public async Task<bool> CerrarCajaAsync(int idAperturaCaja, double montoCierre, string observaciones = "")
        {
            using var transaction = await _farmaDbContext.Database.BeginTransactionAsync();
            try
            {
                if (montoCierre < 0)
                    throw new ArgumentException("El monto de cierre no puede ser negativo");

                var apertura = await _farmaDbContext.AperturaCaja
                    .FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);

                if (apertura == null)
                    throw new KeyNotFoundException("No se encontró la apertura de caja");

                if (apertura.Activa != true)
                    throw new InvalidOperationException("La apertura ya está cerrada.");

                apertura.FechaCierre = DateTime.Now;
                apertura.MontoCierre = montoCierre;
                apertura.Activa = false;

                if (!string.IsNullOrWhiteSpace(observaciones))
                {
                    apertura.Observaciones = string.IsNullOrWhiteSpace(apertura.Observaciones)
                        ? observaciones
                        : $"{apertura.Observaciones}. Cierre: {observaciones}";
                }

                _farmaDbContext.Entry(apertura).State = EntityState.Modified;
                await _farmaDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cerrar caja: {ex.Message}");
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<AperturaCaja?> GetAperturaCajaActivaAsync(int idCaja)
        {
            try
            {
                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .FirstOrDefaultAsync(a => a.IdCaja == idCaja && a.Activa == true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAperturaCajaActivaAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener apertura activa: {ex.Message}", ex);
            }
        }

        public async Task<List<AperturaCaja>> GetAperturasCajaActivasAsync()
        {
            try
            {
                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.Activa == true)
                    .OrderBy(a => a.FechaApertura)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetAperturasCajaActivasAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener aperturas activas: {ex.Message}", ex);
            }
        }

        public async Task<bool> TieneCajaAbiertaAsync(int idPersona)
        {
            try
            {
                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .AnyAsync(a => a.IdPersona == idPersona && a.Activa == true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en TieneCajaAbiertaAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al verificar caja abierta: {ex.Message}", ex);
            }
        }

        public async Task<AperturaCaja?> GetCajaAbiertaPorPersonaAsync(int idPersona)
        {
            try
            {
                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .FirstOrDefaultAsync(a => a.IdPersona == idPersona && a.Activa == true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetCajaAbiertaPorPersonaAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener caja abierta por persona: {ex.Message}", ex);
            }
        }

        // Métodos de consulta adicionales...
        public async Task<List<AperturaCaja>> GetByPersonaAsync(int idPersona)
        {
            try
            {
                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
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
                throw new InvalidOperationException($"Error al obtener aperturas por persona: {ex.Message}", ex);
            }
        }

        public async Task<List<AperturaCaja>> GetByCajaAsync(int idCaja)
        {
            try
            {
                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.IdCaja == idCaja)
                    .OrderByDescending(a => a.FechaApertura)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByCajaAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener aperturas por caja: {ex.Message}", ex);
            }
        }

        public async Task<List<AperturaCaja>> GetByFechaAsync(DateTime fecha)
        {
            try
            {
                var inicio = fecha.Date;
                var fin = inicio.AddDays(1);

                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.FechaApertura >= inicio && a.FechaApertura < fin)
                    .OrderByDescending(a => a.FechaApertura)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByFechaAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener aperturas por fecha: {ex.Message}", ex);
            }
        }

        public async Task<List<AperturaCaja>> GetByRangoFechasAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.FechaApertura >= fechaInicio && a.FechaApertura <= fechaFin)
                    .OrderByDescending(a => a.FechaApertura)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetByRangoFechasAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener aperturas por rango de fechas: {ex.Message}", ex);
            }
        }

        public async Task<Dictionary<string, object>> GetEstadisticasCajaAsync(int idCaja, DateTime? fechaInicio = null, DateTime? fechaFin = null)
        {
            try
            {
                var query = _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.Factura)
                    .Where(a => a.IdCaja == idCaja);

                if (fechaInicio.HasValue)
                    query = query.Where(a => a.FechaApertura >= fechaInicio.Value);
                if (fechaFin.HasValue)
                    query = query.Where(a => a.FechaApertura <= fechaFin.Value);

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
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetEstadisticasCajaAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener estadísticas: {ex.Message}", ex);
            }
        }

        public async Task<List<dynamic>> GetResumenCajasAsync(DateTime? fecha = null)
        {
            try
            {
                var f = fecha ?? DateTime.Today;
                var inicio = f.Date;
                var fin = inicio.AddDays(1);

                return await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.IdCajaNavigation)
                        .ThenInclude(c => c.IdSucursalNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.FechaApertura >= inicio && a.FechaApertura < fin)
                    .Select(a => new
                    {
                        a.IdCaja,
                        NombreCaja = a.IdCajaNavigation.NombreCaja ?? "Sin nombre",
                        Sucursal = a.IdCajaNavigation.IdSucursalNavigation.NombreSucursal ?? "Sin sucursal",
                        Responsable = $"{a.IdPersonaNavigation.Nombre ?? ""} {a.IdPersonaNavigation.Apellido ?? ""}".Trim(),
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetResumenCajasAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al obtener resumen de cajas: {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidarMontosCajaAsync(int idAperturaCaja)
        {
            try
            {
                var apertura = await _farmaDbContext.AperturaCaja
                    .AsNoTracking()
                    .Include(a => a.Factura)
                    .FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);

                if (apertura == null) return false;

                var montoEsperado = (apertura.MontoApertura ?? 0) +
                                   (apertura.Factura?.Sum(f => f.Total ?? 0) ?? 0);

                return Math.Abs((apertura.MontoCierre ?? 0) - montoEsperado) < 0.01;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en ValidarMontosCajaAsync: {ex.Message}");
                throw new InvalidOperationException($"Error al validar montos: {ex.Message}", ex);
            }
        }
    }
}