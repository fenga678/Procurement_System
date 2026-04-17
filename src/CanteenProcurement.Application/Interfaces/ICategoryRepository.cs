using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Application.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<int> CreateAsync(Category category, CancellationToken cancellationToken = default);

    Task<int> UpdateAsync(Category category, CancellationToken cancellationToken = default);

    Task<int> UpdateStatusAsync(string code, bool status, CancellationToken cancellationToken = default);

    Task<(bool CanDelete, string? Message)> CanDeleteAsync(string code, CancellationToken cancellationToken = default);

    Task<int> DeleteAsync(string code, CancellationToken cancellationToken = default);
}
