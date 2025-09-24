using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.VentaService
{
    public interface IVentaService
    {
        // VALIDACIONES PREVIAS A LA VENTA
        Task<bool> ValidarUsuarioParaVentaAsync(int idPersona);
        Task<bool> ValidarInventarioAsignadoAsync(int idPersona);
        Task<bool> ValidarCajaActivaAsync(int idPersona);
        Task<bool> ValidarHorarioTrabajoAsync(int idPersona);
        Task<AperturaCaja?> ObtenerCajaActivaAsync(int idPersona);
        Task<Inventario?> ObtenerInventarioAsignadoAsync(int idPersona);

        // VALIDACIONES DE PRODUCTOS
        Task<bool> ValidarStockDisponibleAsync(int idInventario, int idProducto, int cantidad);
        Task<List<InventarioProducto>> ObtenerProductosDisponiblesAsync(int idInventario, string? busqueda = null);
        Task<InventarioProducto?> ObtenerProductoEnInventarioAsync(int idInventario, int idProducto);

        // OPERACIONES DE VENTA
        Task<int> CrearFacturaAsync(VentaModel ventaModel);
        Task<bool> ActualizarInventarioAsync(List<VentaDetalleModel> detalles, int idInventario);
        Task<bool> ActualizarCajaAsync(int idAperturaCaja, double montoVenta);
        Task<bool> AnularFacturaAsync(int idFactura, string motivo);

        // CONSULTAS
        Task<Factura?> ObtenerFacturaPorIdAsync(int idFactura);
        Task<List<Factura>> ObtenerHistorialVentasAsync(int? idPersona = null, DateTime? fechaInicio = null, DateTime? fechaFin = null);
        Task<List<FacturaDetalle>> ObtenerDetallesFacturaAsync(int idFactura);

        // REPORTES
        Task<Dictionary<string, object>> ObtenerEstadisticasVentasDiariasAsync(int idPersona);
        Task<List<dynamic>> ObtenerProductosMasVendidosAsync(int idInventario, DateTime fechaInicio, DateTime fechaFin);

        Task<AperturaCaja?> ObtenerAperturaCajaActivaPorPersonaAsync(int idPersona);

        Task<(bool esValida, string mensaje)> ValidarAperturaCajaVsSucursalUsuarioAsync(int idPersona);
    }

    // MODELOS PARA EL SERVICIO
    public class VentaModel
    {
        public int IdPersona { get; set; }
        public int IdAperturaCaja { get; set; }
        public int? IdCliente { get; set; }
        public int IdTipoPago { get; set; }
        public double SubTotal { get; set; }
        public double Impuestos { get; set; }
        public double Descuento { get; set; }
        public double Total { get; set; }
        public string? Observaciones { get; set; }
        public List<VentaDetalleModel> Detalles { get; set; } = new();
    }

    public class VentaDetalleModel
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public double PrecioUnitario { get; set; }
        public double SubTotal { get; set; }
        public double Impuesto { get; set; }
        public double Descuento { get; set; }
        public double Total { get; set; }
    }

    public class ProductoVentaModel
    {
        public int IdProducto { get; set; }
        public string NombreProducto { get; set; } = null!;
        public string? DescripcionProducto { get; set; }
        public double PrecioVenta { get; set; }
        public long CantidadDisponible { get; set; }
        public long StockMinimo { get; set; }
        public string? NombreCategoria { get; set; }
        public string? ImagenUrl { get; set; }
        public bool RequiereReceta { get; set; }
    }
}