using System;
using System.Collections.Generic;

namespace ProyectoFarmaVita.Models;

public partial class VwFacturaCompleta
{
    public int IdFactura { get; set; }

    public int? NumeroFactura { get; set; }

    public DateTime? FechaVenta { get; set; }

    public double? SubTotal { get; set; }

    public double? Impuestos { get; set; }

    public double? Descuento { get; set; }

    public double? Total { get; set; }

    public string? Observaciones { get; set; }

    public int? IdCliente { get; set; }

    public string? NombreCliente { get; set; }

    public string? ApellidoCliente { get; set; }

    public string NombreCompletoCliente { get; set; } = null!;

    public string? NitCliente { get; set; }

    public long? DpiCliente { get; set; }

    public string? TelefonoCliente { get; set; }

    public string? EmailCliente { get; set; }

    public string? DireccionCliente { get; set; }

    public string? TipoCliente { get; set; }

    public bool? EsClienteFrecuente { get; set; }

    public string? RazonSocial { get; set; }

    public string? NombrePago { get; set; }

    public string? EstadoFactura { get; set; }

    public int? IdAperturaCaja { get; set; }

    public string? NombreCajero { get; set; }

    public string? ApellidoCajero { get; set; }

    public string NombreCompletoCajero { get; set; } = null!;

    public string? NombreCaja { get; set; }

    public string? NombreSucursal { get; set; }

    public string? EmailSucursal { get; set; }

    public TimeOnly? HorarioApertura { get; set; }

    public TimeOnly? HorarioCierre { get; set; }

    public string? DireccionSucursal { get; set; }

    public int? TelefonoSucursal { get; set; }
}
