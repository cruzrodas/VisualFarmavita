﻿using System;
using System.Collections.Generic;

namespace ProyectoFarmaVita.Models;

public partial class FacturaDetalle
{
    public int IdFacturaDetalle { get; set; }

    public int? IdProducto { get; set; }

    public int? Cantidad { get; set; }

    public double? PrecioUnitario { get; set; }

    public double? SubTotal { get; set; }

    public double? Impuesto { get; set; }

    public double? Descuento { get; set; }

    public double? Total { get; set; }

    public int? IdFactura { get; set; }

    public virtual Factura? IdFacturaNavigation { get; set; }

    public virtual Producto? IdProductoNavigation { get; set; }
}
