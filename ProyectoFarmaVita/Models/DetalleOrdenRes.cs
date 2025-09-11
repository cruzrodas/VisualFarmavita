using System;
using System.Collections.Generic;

namespace ProyectoFarmaVita.Models;

public partial class DetalleOrdenRes
{
    public int IdDetalle { get; set; }

    public int? IdProducto { get; set; }

    public int? CantidadSolicitada { get; set; }

    public double? PrecioUnitario { get; set; }

    public double? Subtotal { get; set; }

    public decimal? Total { get; set; }

    public int? IdOrden { get; set; }

    public decimal? Descuento { get; set; }

    public decimal? Impuesto { get; set; }

    public virtual OrdenRestablecimiento? IdOrdenNavigation { get; set; }

    public virtual Producto? IdProductoNavigation { get; set; }
}
