using System;
using System.Collections.Generic;

namespace ProyectoFarmaVita.Models;

public partial class VwFacturaDetalleCompleta
{
    public int IdFacturaDetalle { get; set; }

    public int? IdFactura { get; set; }

    public int? Cantidad { get; set; }

    public double? PrecioUnitario { get; set; }

    public double? SubTotal { get; set; }

    public double? Impuesto { get; set; }

    public double? Descuento { get; set; }

    public double? Total { get; set; }

    public int IdProducto { get; set; }

    public string? NombreProducto { get; set; }

    public string? DescrpcionProducto { get; set; }

    public string? UnidadMedida { get; set; }

    public double? PrecioVenta { get; set; }

    public bool? RequiereReceta { get; set; }

    public bool? MedicamentoControlado { get; set; }

    public string? NombreCategoria { get; set; }

    public string? DescripcionCategoria { get; set; }

    public double? SubtotalCalculado { get; set; }

    public double? ImpuestoCalculado { get; set; }
}
