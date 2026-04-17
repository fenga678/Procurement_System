using CanteenProcurement.Application.DTOs;

namespace CanteenProcurement.Application.Interfaces;

public interface ITaskPlanningService
{
    Task<GeneratePlanResultDto> GenerateAsync(int taskId, CancellationToken cancellationToken = default);
}
