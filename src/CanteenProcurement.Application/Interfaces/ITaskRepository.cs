using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Application.Interfaces;

public interface ITaskRepository
{
    Task<IReadOnlyList<ProcurementTask>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ProcurementTask?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> CreateAsync(ProcurementTask task, CancellationToken cancellationToken = default);

    Task<int> UpdateStatusAsync(int taskId, Core.Entities.TaskStatus status, CancellationToken cancellationToken = default);

    Task<int> DeleteAsync(int taskId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcurementDetail>> GetDetailsAsync(int taskId, CancellationToken cancellationToken = default);

    Task ReplaceDetailsAsync(int taskId, IReadOnlyList<ProcurementDetail> details, CancellationToken cancellationToken = default);

    Task<bool> HasFixedAmountColumnsAsync(CancellationToken cancellationToken = default);

    Task SaveFixedAmountsAsync(int taskId, IReadOnlyDictionary<string, decimal> fixedAmounts, CancellationToken cancellationToken = default);

    Task<Dictionary<string, decimal>> GetFixedAmountsAsync(int taskId, CancellationToken cancellationToken = default);
}
