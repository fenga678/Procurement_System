using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Application.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync(string? categoryCode = null, string? keyword = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<int> CreateAsync(Product product, CancellationToken cancellationToken = default);

    Task<int> UpdateAsync(Product product, CancellationToken cancellationToken = default);

    Task<int> UpdateStatusAsync(int id, bool status, CancellationToken cancellationToken = default);

    Task<(bool CanDelete, string? Message)> CanDeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
