using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;
using ProyectoFarmaVita.Services.VentaService;

namespace ProyectoFarmaVita.Services.VentaService
{
    public class SVentaService : IVentaService
    {
        private readonly IDbContextFactory<FarmaDbContext> _contextFactory;
        private readonly ILogger<SVentaService> _logger;

        public SVentaService(IDbContextFactory<FarmaDbContext> contextFactory, ILogger<SVentaService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        #region VALIDACIONES PREVIAS A LA VENTA

        public async Task<bool> ValidarUsuarioParaVentaAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var persona = await context.Persona
                    .Include(p => p.IdRoolNavigation)
                    .FirstOrDefaultAsync(p => p.IdPersona == idPersona && p.Activo == true);

                if (persona == null)
                {
                    _logger.LogWarning($"Persona {idPersona} no encontrada o inactiva");
                    return false;
                }

                // Verificar si el rol puede realizar ventas
                var rolesTienePermisoVenta = new[] { "Cajero", "Farmaceuta", "Vendedor", "Gerente", "Administrador" };
                var tienePermiso = rolesTienePermisoVenta.Contains(persona.IdRoolNavigation?.TipoRol);

                _logger.LogInformation($"Usuario {idPersona} - Rol: {persona.IdRoolNavigation?.TipoRol} - Permiso: {tienePermiso}");
                return tienePermiso;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validando usuario {idPersona}");
                return false;
            }
        }

        public async Task<bool> ValidarInventarioAsignadoAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var persona = await context.Persona
                    .FirstOrDefaultAsync(p => p.IdPersona == idPersona);

                if (persona?.IdSucursal == null)
                {
                    _logger.LogWarning($"Usuario {idPersona} - No tiene sucursal asignada");
                    return false;
                }

                var sucursal = await context.Sucursal
                    .Include(s => s.IdInventarioNavigation)
                    .FirstOrDefaultAsync(s => s.IdSucursal == persona.IdSucursal);

                var tieneInventario = sucursal?.IdInventarioNavigation != null;
                _logger.LogInformation($"Usuario {idPersona} - Inventario asignado: {tieneInventario}");
                return tieneInventario;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validando inventario para usuario {idPersona}");
                return false;
            }
        }

        public async Task<bool> ValidarCajaActivaAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                _logger.LogInformation($"🔍 Validando caja activa para persona ID: {idPersona}");

                // Buscar TODAS las aperturas de caja para esta persona
                var todasLasAperturas = await context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.IdPersona == idPersona)
                    .OrderByDescending(a => a.FechaApertura)
                    .ToListAsync();

                _logger.LogInformation($"📊 Total aperturas encontradas para persona {idPersona}: {todasLasAperturas.Count}");

                foreach (var apertura in todasLasAperturas)
                {
                    _logger.LogInformation($"  - Apertura ID: {apertura.IdAperturaCaja}, " +
                                         $"Caja: {apertura.IdCajaNavigation?.NombreCaja}, " +
                                         $"Activa: {apertura.Activa}, " +
                                         $"Fecha Apertura: {apertura.FechaApertura}, " +
                                         $"Fecha Cierre: {apertura.FechaCierre}");
                }

                // Buscar caja activa con criterios flexibles
                var cajaActiva = await context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.IdPersona == idPersona &&
                               (a.Activa == true) &&
                               a.FechaCierre == null)
                    .OrderByDescending(a => a.FechaApertura)
                    .FirstOrDefaultAsync();

                if (cajaActiva != null)
                {
                    _logger.LogInformation($"✅ Caja activa encontrada - ID: {cajaActiva.IdAperturaCaja}, " +
                                         $"Caja: {cajaActiva.IdCajaNavigation?.NombreCaja}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"❌ No se encontró caja activa para persona {idPersona}");

                    // Buscar la apertura más reciente para diagnóstico
                    var ultimaApertura = todasLasAperturas.FirstOrDefault();
                    if (ultimaApertura != null)
                    {
                        _logger.LogWarning($"💡 Última apertura encontrada: " +
                                         $"ID: {ultimaApertura.IdAperturaCaja}, " +
                                         $"Activa: {ultimaApertura.Activa}, " +
                                         $"Fecha Cierre: {ultimaApertura.FechaCierre}");
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error validando caja activa para usuario {idPersona}");
                return false;
            }
        }

        public async Task<bool> ValidarHorarioTrabajoAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var ahora = DateTime.Now;
                var fechaHoy = DateOnly.FromDateTime(ahora);
                var horaActual = TimeOnly.FromDateTime(ahora);

                var asignacionActiva = await context.AsignacionTurno
                    .Include(a => a.IdTurnoNavigation)
                    .Where(a =>
                        a.IdPersona == idPersona &&
                        a.FechaInicio <= fechaHoy &&
                        (a.FechaFin == null || a.FechaFin >= fechaHoy))
                    .FirstOrDefaultAsync();

                if (asignacionActiva?.IdTurnoNavigation == null)
                {
                    _logger.LogWarning($"Usuario {idPersona} no tiene asignación de turno activa");
                    return false;
                }

                var turno = asignacionActiva.IdTurnoNavigation;

                if (turno.Activo != true)
                {
                    _logger.LogWarning($"Usuario {idPersona} tiene turno inactivo");
                    return false;
                }

                var horaInicio = TimeOnly.FromDateTime(turno.HoraInicio ?? DateTime.MinValue);
                var horaFin = TimeOnly.FromDateTime(turno.HoraFin ?? DateTime.MaxValue);

                var estaEnHorario = horaActual >= horaInicio && horaActual <= horaFin;
                _logger.LogInformation($"Usuario {idPersona} - Horario: {horaInicio}-{horaFin}, Actual: {horaActual}, En horario: {estaEnHorario}");
                return estaEnHorario;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validando horario para usuario {idPersona}");
                return false;
            }
        }

        public async Task<AperturaCaja?> ObtenerCajaActivaAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                _logger.LogInformation($"🔍 Obteniendo caja activa para persona ID: {idPersona}");

                var cajaActiva = await context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.IdPersona == idPersona &&
                               a.Activa == true &&
                               a.FechaCierre == null)
                    .OrderByDescending(a => a.FechaApertura)
                    .FirstOrDefaultAsync();

                if (cajaActiva != null)
                {
                    _logger.LogInformation($"✅ Caja activa obtenida - ID: {cajaActiva.IdAperturaCaja}");
                }
                else
                {
                    _logger.LogWarning($"❌ No se pudo obtener caja activa para persona {idPersona}");
                }

                return cajaActiva;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error obteniendo caja activa para usuario {idPersona}");
                return null;
            }
        }

        public async Task<Inventario?> ObtenerInventarioAsignadoAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var persona = await context.Persona
                    .FirstOrDefaultAsync(p => p.IdPersona == idPersona);

                if (persona?.IdSucursal == null)
                {
                    return null;
                }

                var sucursal = await context.Sucursal
                    .Include(s => s.IdInventarioNavigation)
                    .FirstOrDefaultAsync(s => s.IdSucursal == persona.IdSucursal);

                return sucursal?.IdInventarioNavigation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo inventario para usuario {idPersona}");
                return null;
            }
        }

        public async Task<AperturaCaja?> ObtenerAperturaCajaActivaPorPersonaAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                _logger.LogInformation($"🔍 Obteniendo apertura de caja activa para persona ID: {idPersona}");

                var aperturaCajaActiva = await context.AperturaCaja
                    .Include(a => a.IdCajaNavigation)
                    .Include(a => a.IdPersonaNavigation)
                    .Where(a => a.IdPersona == idPersona &&
                               a.Activa == true &&
                               a.FechaCierre == null)
                    .OrderByDescending(a => a.FechaApertura)
                    .FirstOrDefaultAsync();

                if (aperturaCajaActiva != null)
                {
                    _logger.LogInformation($"✅ Apertura de caja activa encontrada - ID: {aperturaCajaActiva.IdAperturaCaja}, " +
                                         $"Caja: {aperturaCajaActiva.IdCajaNavigation?.NombreCaja}");
                }
                else
                {
                    _logger.LogWarning($"❌ No se encontró apertura de caja activa para persona {idPersona}");
                }

                return aperturaCajaActiva;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error obteniendo apertura de caja activa para usuario {idPersona}");
                return null;
            }
        }

        #endregion

        #region VALIDACIONES DE PRODUCTOS

        public async Task<bool> ValidarStockDisponibleAsync(int idInventario, int idProducto, int cantidad)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var inventarioProducto = await context.InventarioProducto
                    .FirstOrDefaultAsync(ip =>
                        ip.IdInventario == idInventario &&
                        ip.IdProducto == idProducto);

                if (inventarioProducto == null)
                {
                    _logger.LogWarning($"Producto {idProducto} no encontrado en inventario {idInventario}");
                    return false;
                }

                var stockDisponible = inventarioProducto.Cantidad ?? 0;
                var tieneStock = stockDisponible >= cantidad;

                _logger.LogInformation($"Producto {idProducto} - Stock: {stockDisponible}, Solicitado: {cantidad}, Disponible: {tieneStock}");
                return tieneStock;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validando stock para producto {idProducto}");
                return false;
            }
        }

        public async Task<List<InventarioProducto>> ObtenerProductosDisponiblesAsync(int idInventario, string? busqueda = null)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var query = context.InventarioProducto
                    .Include(ip => ip.IdProductoNavigation)
                    .ThenInclude(p => p.IdCategoriaNavigation)
                    .Where(ip =>
                        ip.IdInventario == idInventario &&
                        (ip.Cantidad ?? 0) > 0 &&
                        ip.IdProductoNavigation.Activo == true);

                if (!string.IsNullOrEmpty(busqueda))
                {
                    query = query.Where(ip =>
                        ip.IdProductoNavigation.NombreProducto.Contains(busqueda) ||
                        (ip.IdProductoNavigation.DescrpcionProducto != null &&
                         ip.IdProductoNavigation.DescrpcionProducto.Contains(busqueda)));
                }

                return await query
                    .OrderBy(ip => ip.IdProductoNavigation.NombreProducto)
                    .Take(50)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo productos disponibles");
                return new List<InventarioProducto>();
            }
        }

        public async Task<InventarioProducto?> ObtenerProductoEnInventarioAsync(int idInventario, int idProducto)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                return await context.InventarioProducto
                    .Include(ip => ip.IdProductoNavigation)
                    .FirstOrDefaultAsync(ip =>
                        ip.IdInventario == idInventario &&
                        ip.IdProducto == idProducto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo producto {idProducto} en inventario {idInventario}");
                return null;
            }
        }

        #endregion

        #region OPERACIONES DE VENTA

        public async Task<int> CrearFacturaAsync(VentaModel ventaModel)
        {
            var strategy = _contextFactory.CreateDbContext().Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var context = _contextFactory.CreateDbContext();
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    // Obtener el siguiente número de factura
                    var ultimoNumero = await context.Factura
                        .MaxAsync(f => (int?)f.NumeroFactura) ?? 0;

                    // Crear factura
                    var factura = new Factura
                    {
                        FechaVenta = DateTime.Now,
                        SubTotal = ventaModel.SubTotal,
                        Impuestos = ventaModel.Impuestos,
                        Descuento = ventaModel.Descuento,
                        Total = ventaModel.Total,
                        NumeroFactura = ultimoNumero + 1,
                        Observaciones = ventaModel.Observaciones,
                        IdTipoPago = ventaModel.IdTipoPago,
                        IdAperturaCaja = ventaModel.IdAperturaCaja,
                        IdCliente = ventaModel.IdCliente,
                        IdEstado = await ObtenerEstadoActivoAsync(context)
                    };

                    context.Factura.Add(factura);
                    await context.SaveChangesAsync();

                    // Crear detalles de factura
                    foreach (var detalle in ventaModel.Detalles)
                    {
                        var facturaDetalle = new FacturaDetalle
                        {
                            IdFactura = factura.IdFactura,
                            IdProducto = detalle.IdProducto,
                            Cantidad = detalle.Cantidad,
                            PrecioUnitario = detalle.PrecioUnitario,
                            SubTotal = detalle.SubTotal,
                            Impuesto = detalle.Impuesto,
                            Descuento = detalle.Descuento,
                            Total = detalle.Total
                        };

                        context.FacturaDetalle.Add(facturaDetalle);
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Factura {factura.NumeroFactura} creada exitosamente");
                    return factura.IdFactura;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creando factura");
                    throw;
                }
            });
        }

        public async Task<bool> ActualizarInventarioAsync(List<VentaDetalleModel> detalles, int idInventario)
        {
            var strategy = _contextFactory.CreateDbContext().Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var context = _contextFactory.CreateDbContext();
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var detalle in detalles)
                    {
                        var inventarioProducto = await context.InventarioProducto
                            .FirstOrDefaultAsync(ip =>
                                ip.IdInventario == idInventario &&
                                ip.IdProducto == detalle.IdProducto);

                        if (inventarioProducto != null)
                        {
                            var stockAnterior = inventarioProducto.Cantidad ?? 0;
                            inventarioProducto.Cantidad = stockAnterior - detalle.Cantidad;

                            if (inventarioProducto.Cantidad < 0)
                            {
                                throw new InvalidOperationException($"Stock insuficiente para producto {detalle.IdProducto}");
                            }

                            context.InventarioProducto.Update(inventarioProducto);
                            _logger.LogInformation($"Producto {detalle.IdProducto}: {stockAnterior} - {detalle.Cantidad} = {inventarioProducto.Cantidad}");
                        }
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error actualizando inventario");
                    return false;
                }
            });
        }

        public async Task<bool> ActualizarCajaAsync(int idAperturaCaja, double montoVenta)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var aperturaCaja = await context.AperturaCaja
                    .FirstOrDefaultAsync(a => a.IdAperturaCaja == idAperturaCaja);

                if (aperturaCaja != null)
                {
                    _logger.LogInformation($"Venta de Q{montoVenta:F2} registrada en caja {idAperturaCaja}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error actualizando caja {idAperturaCaja}");
                return false;
            }
        }

        public async Task<bool> AnularFacturaAsync(int idFactura, string motivo)
        {
            var strategy = _contextFactory.CreateDbContext().Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var context = _contextFactory.CreateDbContext();
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    var factura = await context.Factura
                        .Include(f => f.FacturaDetalle)
                        .FirstOrDefaultAsync(f => f.IdFactura == idFactura);

                    if (factura == null)
                        return false;

                    // Cambiar estado a anulado
                    var estadoAnulado = await context.Estado
                        .FirstOrDefaultAsync(e => e.Estado1.ToLower().Contains("anulado"));

                    if (estadoAnulado != null)
                    {
                        factura.IdEstado = estadoAnulado.IdEstado;
                        factura.Observaciones = $"{factura.Observaciones} | ANULADO: {motivo}";
                        context.Factura.Update(factura);
                    }

                    // Restaurar inventario
                    foreach (var detalle in factura.FacturaDetalle)
                    {
                        var inventarioProducto = await context.InventarioProducto
                            .FirstOrDefaultAsync(ip => ip.IdProducto == detalle.IdProducto);

                        if (inventarioProducto != null)
                        {
                            inventarioProducto.Cantidad = (inventarioProducto.Cantidad ?? 0) + (detalle.Cantidad ?? 0);
                            context.InventarioProducto.Update(inventarioProducto);
                        }
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Factura {idFactura} anulada: {motivo}");
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error anulando factura {idFactura}");
                    return false;
                }
            });
        }

        #endregion

        #region CONSULTAS

        public async Task<Factura?> ObtenerFacturaPorIdAsync(int idFactura)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                return await context.Factura
                    .Include(f => f.IdClienteNavigation)
                    .Include(f => f.IdTipoPagoNavigation)
                    .Include(f => f.IdEstadoNavigation)
                    .Include(f => f.IdAperturaCajaNavigation)
                        .ThenInclude(a => a.IdPersonaNavigation)
                    .Include(f => f.IdAperturaCajaNavigation)
                        .ThenInclude(a => a.IdCajaNavigation)
                    .Include(f => f.FacturaDetalle)
                        .ThenInclude(fd => fd.IdProductoNavigation)
                            .ThenInclude(p => p.IdCategoriaNavigation)
                    .FirstOrDefaultAsync(f => f.IdFactura == idFactura);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo factura {idFactura}");
                return null;
            }
        }

        public async Task<List<Factura>> ObtenerHistorialVentasAsync(int? idPersona = null, DateTime? fechaInicio = null, DateTime? fechaFin = null)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var query = context.Factura
                    .Include(f => f.IdClienteNavigation)
                    .Include(f => f.IdTipoPagoNavigation)
                    .Include(f => f.IdEstadoNavigation)
                    .Include(f => f.IdAperturaCajaNavigation)
                        .ThenInclude(a => a.IdPersonaNavigation)
                    .Include(f => f.IdAperturaCajaNavigation)
                        .ThenInclude(a => a.IdCajaNavigation)
                    .AsQueryable();

                if (idPersona.HasValue)
                {
                    query = query.Where(f => f.IdAperturaCajaNavigation.IdPersona == idPersona);
                }

                if (fechaInicio.HasValue)
                {
                    query = query.Where(f => f.FechaVenta >= fechaInicio);
                }

                if (fechaFin.HasValue)
                {
                    query = query.Where(f => f.FechaVenta <= fechaFin);
                }

                return await query
                    .OrderByDescending(f => f.FechaVenta)
                    .Take(100)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo historial de ventas");
                return new List<Factura>();
            }
        }

        public async Task<List<FacturaDetalle>> ObtenerDetallesFacturaAsync(int idFactura)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                return await context.FacturaDetalle
                    .Include(fd => fd.IdProductoNavigation)
                        .ThenInclude(p => p.IdCategoriaNavigation)
                    .Where(fd => fd.IdFactura == idFactura)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo detalles de factura {idFactura}");
                return new List<FacturaDetalle>();
            }
        }

        #endregion

        #region REPORTES

        public async Task<Dictionary<string, object>> ObtenerEstadisticasVentasDiariasAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var hoy = DateTime.Today;
                var mañana = hoy.AddDays(1);

                var ventasHoy = await context.Factura
                    .Include(f => f.IdAperturaCajaNavigation)
                    .Where(f =>
                        f.IdAperturaCajaNavigation.IdPersona == idPersona &&
                        f.FechaVenta >= hoy &&
                        f.FechaVenta < mañana &&
                        f.IdEstado != null)
                    .ToListAsync();

                var estadoAnulado = await context.Estado
                    .FirstOrDefaultAsync(e => e.Estado1.ToLower().Contains("anulado"));

                var ventasActivas = ventasHoy.Where(v => v.IdEstado != estadoAnulado?.IdEstado).ToList();

                return new Dictionary<string, object>
                {
                    ["TotalVentas"] = ventasActivas.Count,
                    ["VentasAnuladas"] = ventasHoy.Count - ventasActivas.Count,
                    ["MontoTotal"] = ventasActivas.Sum(v => v.Total ?? 0),
                    ["PromedioVenta"] = ventasActivas.Any() ? ventasActivas.Average(v => v.Total ?? 0) : 0,
                    ["VentaMasAlta"] = ventasActivas.Any() ? ventasActivas.Max(v => v.Total ?? 0) : 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo estadísticas para usuario {idPersona}");
                return new Dictionary<string, object>();
            }
        }

        public async Task<List<dynamic>> ObtenerProductosMasVendidosAsync(int idInventario, DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var query = from fd in context.FacturaDetalle
                            join f in context.Factura on fd.IdFactura equals f.IdFactura
                            join ip in context.InventarioProducto on fd.IdProducto equals ip.IdProducto
                            join p in context.Producto on fd.IdProducto equals p.IdProducto
                            where ip.IdInventario == idInventario &&
                                  f.FechaVenta >= fechaInicio &&
                                  f.FechaVenta <= fechaFin
                            group fd by new { fd.IdProducto, p.NombreProducto } into g
                            select new
                            {
                                IdProducto = g.Key.IdProducto,
                                NombreProducto = g.Key.NombreProducto,
                                CantidadVendida = g.Sum(x => x.Cantidad ?? 0),
                                MontoTotal = g.Sum(x => x.Total ?? 0)
                            };

                return await query
                    .OrderByDescending(x => x.CantidadVendida)
                    .Take(10)
                    .Cast<dynamic>()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo productos más vendidos");
                return new List<dynamic>();
            }
        }

        #endregion

        #region MÉTODOS AUXILIARES

        private async Task<int> ObtenerEstadoActivoAsync(FarmaDbContext context)
        {
            var estado = await context.Estado
                .FirstOrDefaultAsync(e => e.Estado1.ToLower().Contains("activ"));

            return estado?.IdEstado ?? 1; // Valor por defecto
        }

        #endregion

        #region MÉTODOS DE DIAGNÓSTICO

        public async Task<Dictionary<string, object>> DiagnosticarEstadoCajasAsync(int idPersona)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var diagnostico = new Dictionary<string, object>();

                // Verificar si la persona existe
                var persona = await context.Persona
                    .Include(p => p.IdRoolNavigation)
                    .FirstOrDefaultAsync(p => p.IdPersona == idPersona);

                diagnostico["PersonaExiste"] = persona != null;
                diagnostico["PersonaActiva"] = persona?.Activo == true;
                diagnostico["RolPersona"] = persona?.IdRoolNavigation?.TipoRol ?? "Sin rol";
                diagnostico["SucursalId"] = persona?.IdSucursal;

                if (persona != null)
                {
                    // Obtener todas las aperturas de caja
                    var aperturas = await context.AperturaCaja
                        .Include(a => a.IdCajaNavigation)
                        .Where(a => a.IdPersona == idPersona)
                        .OrderByDescending(a => a.FechaApertura)
                        .ToListAsync();

                    diagnostico["TotalAperturas"] = aperturas.Count;
                    diagnostico["AperturasActivas"] = aperturas.Count(a => a.Activa == true && a.FechaCierre == null);
                    diagnostico["AperturasCerradas"] = aperturas.Count(a => a.FechaCierre != null);

                    // Detalles de la apertura más reciente
                    var ultimaApertura = aperturas.FirstOrDefault();
                    if (ultimaApertura != null)
                    {
                        diagnostico["UltimaApertura"] = new
                        {
                            Id = ultimaApertura.IdAperturaCaja,
                            Caja = ultimaApertura.IdCajaNavigation?.NombreCaja,
                            FechaApertura = ultimaApertura.FechaApertura,
                            FechaCierre = ultimaApertura.FechaCierre,
                            Activa = ultimaApertura.Activa,
                            MontoApertura = ultimaApertura.MontoApertura
                        };
                    }

                    // Verificar cajas disponibles para esta persona
                    var cajasDisponibles = await context.Caja
                        .Include(c => c.IdSucursalNavigation)
                        .Where(c => c.Activa == true)
                        .ToListAsync();

                    diagnostico["CajasDisponibles"] = cajasDisponibles.Count;

                    // Verificar si hay cajas en la misma sucursal
                    if (persona.IdSucursal.HasValue)
                    {
                        var cajasEnSucursal = cajasDisponibles.Count(c => c.IdSucursal == persona.IdSucursal);
                        diagnostico["CajasEnMiSucursal"] = cajasEnSucursal;
                    }
                }

                return diagnostico;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en diagnóstico para usuario {idPersona}");
                return new Dictionary<string, object> { ["Error"] = ex.Message };
            }
        }

        #endregion



    }
}