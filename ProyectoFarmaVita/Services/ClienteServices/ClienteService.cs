using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;
using ProyectoFarmaVita.Services.ClienteServices;

namespace ProyectoFarmaVita.Services.ClienteServices
{
    public class ClienteService : IClienteService
    {
        private readonly IDbContextFactory<FarmaDbContext> _contextFactory;
        private readonly ILogger<ClienteService> _logger;

        public ClienteService(IDbContextFactory<FarmaDbContext> contextFactory, ILogger<ClienteService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<List<Cliente>> GetAllAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.Cliente
                    .Where(c => c.Activo)
                    .OrderBy(c => c.NombreCliente)
                    .ThenBy(c => c.ApellidoCliente)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todos los clientes");
                return new List<Cliente>();
            }
        }

        public async Task<Cliente> GetByIdAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.Cliente
                    .FirstOrDefaultAsync(c => c.IdCliente == id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener cliente con ID {id}");
                return null;
            }
        }

        public async Task<MPaginatedResult<Cliente>> GetPaginatedAsync(int page, int pageSize, string searchTerm = "")
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var query = context.Cliente.AsQueryable();

                // Aplicar filtro de búsqueda
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(c =>
                        c.NombreCliente.ToLower().Contains(searchTerm) ||
                        c.ApellidoCliente.ToLower().Contains(searchTerm) ||
                        (c.NitCliente != null && c.NitCliente.Contains(searchTerm)) ||
                        (c.EmailCliente != null && c.EmailCliente.ToLower().Contains(searchTerm)) ||
                        (c.TelefonoCliente != null && c.TelefonoCliente.Contains(searchTerm))
                    );
                }

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderBy(c => c.NombreCliente)
                    .ThenBy(c => c.ApellidoCliente)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new MPaginatedResult<Cliente>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = page,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clientes paginados");
                return new MPaginatedResult<Cliente>
                {
                    Items = new List<Cliente>(),
                    TotalCount = 0,
                    PageNumber = page,
                    PageSize = pageSize
                };
            }
        }

        public async Task<bool> AddUpdateAsync(Cliente cliente)
        {
            var strategy = _contextFactory.CreateDbContext().Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                using var context = _contextFactory.CreateDbContext();
                using var transaction = await context.Database.BeginTransactionAsync();

                try
                {
                    if (cliente.IdCliente == 0)
                    {
                        // Nuevo cliente
                        cliente.FechaCreacion = DateTime.Now;
                        cliente.Activo = true;
                        context.Cliente.Add(cliente);
                        _logger.LogInformation($"Creando nuevo cliente: {cliente.NombreCliente} {cliente.ApellidoCliente}");
                    }
                    else
                    {
                        // Actualizar cliente existente
                        var clienteExistente = await context.Cliente
                            .FirstOrDefaultAsync(c => c.IdCliente == cliente.IdCliente);

                        if (clienteExistente == null)
                        {
                            _logger.LogWarning($"Cliente con ID {cliente.IdCliente} no encontrado");
                            return false;
                        }

                        // Actualizar propiedades
                        clienteExistente.NombreCliente = cliente.NombreCliente;
                        clienteExistente.ApellidoCliente = cliente.ApellidoCliente;
                        clienteExistente.NitCliente = cliente.NitCliente;
                        clienteExistente.DpiCliente = cliente.DpiCliente;
                        clienteExistente.RtuCliente = cliente.RtuCliente;
                        clienteExistente.TelefonoCliente = cliente.TelefonoCliente;
                        clienteExistente.EmailCliente = cliente.EmailCliente;
                        clienteExistente.DireccionCliente = cliente.DireccionCliente;
                        clienteExistente.TipoCliente = cliente.TipoCliente;
                        clienteExistente.EsClienteFrecuente = cliente.EsClienteFrecuente;
                        clienteExistente.RazonSocial = cliente.RazonSocial;
                        clienteExistente.NombreContacto = cliente.NombreContacto;
                        clienteExistente.FechaModificacion = DateTime.Now;
                        clienteExistente.UsuarioModificacion = cliente.UsuarioModificacion;

                        context.Cliente.Update(clienteExistente);
                        _logger.LogInformation($"Actualizando cliente: {cliente.NombreCliente} {cliente.ApellidoCliente}");
                    }

                    await context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, $"Error al guardar cliente: {cliente.NombreCliente} {cliente.ApellidoCliente}");
                    return false;
                }
            });
        }

        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var cliente = await context.Cliente
                    .Include(c => c.Factura)
                    .FirstOrDefaultAsync(c => c.IdCliente == id);

                if (cliente == null)
                {
                    _logger.LogWarning($"Cliente con ID {id} no encontrado");
                    return false;
                }

                // Verificar si tiene facturas asociadas
                if (cliente.Factura.Any())
                {
                    _logger.LogWarning($"No se puede eliminar el cliente {id} porque tiene facturas asociadas");
                    return false;
                }

                // Eliminación lógica
                cliente.Activo = false;
                cliente.FechaModificacion = DateTime.Now;

                context.Cliente.Update(cliente);
                await context.SaveChangesAsync();

                _logger.LogInformation($"Cliente {id} desactivado correctamente");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al eliminar cliente {id}");
                return false;
            }
        }

        public async Task<List<Cliente>> GetClientesFrecuentesAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                return await context.Cliente
                    .Where(c => c.Activo && c.EsClienteFrecuente)
                    .OrderBy(c => c.NombreCliente)
                    .ThenBy(c => c.ApellidoCliente)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener clientes frecuentes");
                return new List<Cliente>();
            }
        }

        public async Task<List<Cliente>> SearchClientesAsync(string searchTerm)
        {
            try
            {
                if (string.IsNullOrEmpty(searchTerm))
                    return new List<Cliente>();

                using var context = _contextFactory.CreateDbContext();
                searchTerm = searchTerm.ToLower();

                return await context.Cliente
                    .Where(c => c.Activo && (
                        c.NombreCliente.ToLower().Contains(searchTerm) ||
                        c.ApellidoCliente.ToLower().Contains(searchTerm) ||
                        (c.NitCliente != null && c.NitCliente.Contains(searchTerm)) ||
                        (c.TelefonoCliente != null && c.TelefonoCliente.Contains(searchTerm))
                    ))
                    .OrderBy(c => c.NombreCliente)
                    .Take(10)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al buscar clientes con término: {searchTerm}");
                return new List<Cliente>();
            }
        }

        public async Task<bool> ExisteClienteConNitAsync(string nit, int? excludeId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(nit))
                    return false;

                using var context = _contextFactory.CreateDbContext();

                var query = context.Cliente.Where(c => c.NitCliente == nit && c.Activo);

                if (excludeId.HasValue)
                {
                    query = query.Where(c => c.IdCliente != excludeId.Value);
                }

                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al verificar NIT: {nit}");
                return false;
            }
        }

        public async Task<bool> ExisteClienteConDpiAsync(long dpi, int? excludeId = null)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var query = context.Cliente.Where(c => c.DpiCliente == dpi && c.Activo);

                if (excludeId.HasValue)
                {
                    query = query.Where(c => c.IdCliente != excludeId.Value);
                }

                return await query.AnyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al verificar DPI: {dpi}");
                return false;
            }
        }
    }
}