using System;
using System.Collections.Generic;

namespace ProyectoFarmaVita.Models;

public partial class Cliente
{
    public int IdCliente { get; set; }

    public string NombreCliente { get; set; } = null!;

    public string? ApellidoCliente { get; set; }

    public string? NitCliente { get; set; }

    public long? DpiCliente { get; set; }

    public string? RtuCliente { get; set; }

    public string? TelefonoCliente { get; set; }

    public string? EmailCliente { get; set; }

    public string? DireccionCliente { get; set; }

    public string TipoCliente { get; set; } = null!;

    public bool EsClienteFrecuente { get; set; }

    public bool Activo { get; set; }

    public DateTime FechaCreacion { get; set; }

    public string? UsuarioCreacion { get; set; }

    public DateTime? FechaModificacion { get; set; }

    public string? UsuarioModificacion { get; set; }

    public string? RazonSocial { get; set; }

    public string? NombreContacto { get; set; }

    public virtual ICollection<Factura> Factura { get; set; } = new List<Factura>();
}
