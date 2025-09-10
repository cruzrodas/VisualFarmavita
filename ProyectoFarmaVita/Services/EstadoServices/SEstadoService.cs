using Microsoft.EntityFrameworkCore;
using ProyectoFarmaVita.Models;

namespace ProyectoFarmaVita.Services.EstadoServices
{
    public class SEstadoService : IEstadoService
    {
        private readonly FarmaDbContext _farmaDbContext;

        public SEstadoService(FarmaDbContext farmaDbContext)
        {
            _farmaDbContext = farmaDbContext;
        }

        public async Task<bool> AddUpdateAsync(Estado estado)
        {
            try
            {
                if (estado.IdEstado > 0)
                {
                    // Buscar el estado existente
                    var existingEstado = await _farmaDbContext.Estado.FindAsync(estado.IdEstado);

                    if (existingEstado != null)
                    {
                        // Actualizar propiedades
                        existingEstado.Estado1 = estado.Estado1;

                        _farmaDbContext.Estado.Update(existingEstado);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    // Crear nuevo estado
                    _farmaDbContext.Estado.Add(estado);
                }

                await _farmaDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en AddUpdateAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAsync(int id_estado)
        {
            try
            {
                var estado = await _farmaDbContext.Estado
                    .Include(e => e.Factura)
                    .Include(e => e.Traslado)
                    .Include(e => e.TrasladoDetalle)
                    .Include(e => e.OrdenRestablecimiento)
                    .FirstOrDefaultAsync(e => e.IdEstado == id_estado);

                if (estado != null)
                {
                    // Verificar si el estado tiene dependencias
                    bool hasDependencies = (estado.Factura?.Any() == true) ||
                                         (estado.Traslado?.Any() == true) ||
                                         (estado.TrasladoDetalle?.Any() == true) ||
                                         (estado.OrdenRestablecimiento?.Any() == true);

                    if (!hasDependencies)
                    {
                        // Eliminar físicamente si no tiene dependencias
                        _farmaDbContext.Estado.Remove(estado);
                        await _farmaDbContext.SaveChangesAsync();
                        return true;
                    }
                    else
                    {
                        // No se puede eliminar porque tiene dependencias
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en DeleteAsync: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Estado>> GetAllAsync()
        {
            return await _farmaDbContext.Estado
                .OrderBy(e => e.Estado1)
                .ToListAsync();
        }

        public async Task<Estado> GetByIdAsync(int id_estado)
        {
            var result = await _farmaDbContext.Estado
                .FirstOrDefaultAsync(e => e.IdEstado == id_estado);

            if (result == null)
            {
                throw new KeyNotFoundException($"No se encontró el estado con ID {id_estado}");
            }

            return result;
        }

        public async Task<MPaginatedResult<Estado>> GetPaginatedAsync(int pageNumber, int pageSize, string searchTerm = "", bool sortAscending = true)
        {
            var query = _farmaDbContext.Estado.AsQueryable();

            // Aplicar filtros de búsqueda
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(e => e.Estado1.Contains(searchTerm));
            }

            // Aplicar ordenamiento
            query = sortAscending
                ? query.OrderBy(e => e.Estado1)
                : query.OrderByDescending(e => e.Estado1);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new MPaginatedResult<Estado>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<Estado> GetByNombreAsync(string nombre)
        {
            return await _farmaDbContext.Estado
                .FirstOrDefaultAsync(e => e.Estado1.ToLower() == nombre.ToLower());
        }

        public async Task<List<Estado>> GetEstadosParaTrasladosAsync()
        {
            // Retornar estados típicos para traslados
            var estadosTraslados = new[] { "Pendiente", "En Proceso", "Completado", "Cancelado" };

            return await _farmaDbContext.Estado
                .Where(e => estadosTraslados.Contains(e.Estado1))
                .OrderBy(e => e.Estado1)
                .ToListAsync();
        }

        public async Task<List<Estado>> GetEstadosParaFacturasAsync()
        {
            // Retornar estados típicos para facturas
            var estadosFacturas = new[] { "Pendiente", "Pagada", "Cancelada", "Anulada" };

            return await _farmaDbContext.Estado
                .Where(e => estadosFacturas.Contains(e.Estado1))
                .OrderBy(e => e.Estado1)
                .ToListAsync();
        }
    }
}