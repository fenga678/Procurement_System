using CanteenProcurement.Application.DTOs;
using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Core.Entities;
using TaskStatus = CanteenProcurement.Core.Entities.TaskStatus;

namespace CanteenProcurement.Wpf.Services;

/// <summary>
/// 任务数据服务 - WPF 层用例服务
/// </summary>
public sealed class TaskDataService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskPlanningService _taskPlanningService;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductRepository _productRepository;

    public TaskDataService(
        ITaskRepository taskRepository,
        ITaskPlanningService taskPlanningService,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository)
    {
        _taskRepository = taskRepository;
        _taskPlanningService = taskPlanningService;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
    }

    public async Task<List<TaskRecord>> GetTasksAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await _taskRepository.GetAllAsync(cancellationToken);
        return tasks.Select(task => new TaskRecord
        {
            Id = task.Id,
            YearMonth = task.YearMonth,
            TotalBudget = task.TotalBudget,
            FloatRate = task.FloatRate,
            Status = (int)task.Status,
            GeneratedAt = task.GeneratedAt,
            CreatedBy = task.CreatedBy ?? string.Empty,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        }).ToList();
    }

    public Task<int> CreateTaskAsync(TaskRecord record, CancellationToken cancellationToken = default)
    {
        ValidateTaskRecord(record);
        return _taskRepository.CreateAsync(new ProcurementTask
        {
            YearMonth = record.YearMonth.Trim(),
            TotalBudget = record.TotalBudget,
            FloatRate = record.FloatRate,
            Status = (TaskStatus)record.Status,
            GeneratedAt = record.GeneratedAt,
            CreatedBy = string.IsNullOrWhiteSpace(record.CreatedBy) ? null : record.CreatedBy.Trim()
        }, cancellationToken);
    }

    public Task<int> UpdateStatusAsync(int taskId, int status)
    {
        return _taskRepository.UpdateStatusAsync(taskId, (TaskStatus)status);
    }

    public Task<int> DeleteTaskAsync(int taskId)
    {
        return _taskRepository.DeleteAsync(taskId);
    }

    public Task<bool> HasFixedAmountColumnsAsync(CancellationToken cancellationToken = default)
    {
        return _taskRepository.HasFixedAmountColumnsAsync(cancellationToken);
    }

    public async Task<List<CategoryBudgetRecord>> GetActiveCategoriesAsync()
    {
        var categories = await _categoryRepository.GetActiveAsync();
        return categories.Select(category => new CategoryBudgetRecord
        {
            Code = category.Code,
            Ratio = category.Ratio,
            FrequencyDays = category.FrequencyDays,
            DailyMinItems = category.DailyMinItems,
            DailyMaxItems = category.DailyMaxItems
        }).ToList();
    }

    public async Task<List<ProductPriceRecord>> GetActiveProductsAsync()
    {
        var products = await _productRepository.GetActiveAsync();
        return products.Select(product => new ProductPriceRecord
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            CategoryCode = product.CategoryCode
        }).ToList();
    }

    public Task ReplaceTaskDetailsAsync(int taskId, List<ProcurementDetailRecord> details, CancellationToken cancellationToken = default)
    {
        var entities = details.Select(detail => new ProcurementDetail
        {
            TaskId = taskId,
            CategoryCode = detail.CategoryCode,
            ProductId = detail.ProductId,
            PurchaseDate = detail.PurchaseDate,
            Price = detail.Price,
            Quantity = detail.Quantity,
            Amount = detail.Amount
        }).ToList();
        return _taskRepository.ReplaceDetailsAsync(taskId, entities, cancellationToken);
    }

    public async Task<List<ProcurementDetailRecord>> GetTaskDetailsAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var details = await _taskRepository.GetDetailsAsync(taskId, cancellationToken);
        return details.Select(detail => new ProcurementDetailRecord
        {
            TaskId = detail.TaskId,
            CategoryCode = detail.CategoryCode,
            CategoryName = detail.Category?.Name ?? string.Empty,
            ProductId = detail.ProductId,
            ProductName = detail.Product?.Name ?? string.Empty,
            Unit = detail.Product?.Unit ?? string.Empty,
            PurchaseDate = detail.PurchaseDate,
            Price = detail.Price,
            Quantity = detail.Quantity,
            Amount = detail.Amount
        }).ToList();
    }

    public Task SaveTaskFixedAmountsAsync(int taskId, Dictionary<string, decimal> fixedAmounts, CancellationToken cancellationToken = default)
    {
        return _taskRepository.SaveFixedAmountsAsync(taskId, fixedAmounts, cancellationToken);
    }

    public Task<Dictionary<string, decimal>> GetTaskFixedAmountsAsync(int taskId, CancellationToken cancellationToken = default)
    {
        return _taskRepository.GetFixedAmountsAsync(taskId, cancellationToken);
    }

    public Task<GeneratePlanResultDto> GeneratePlanAsync(int taskId, CancellationToken cancellationToken = default)
    {
        return _taskPlanningService.GenerateAsync(taskId, cancellationToken);
    }

    private static void ValidateTaskRecord(TaskRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        if (string.IsNullOrWhiteSpace(record.YearMonth) || record.YearMonth.Length != 6 || !record.YearMonth.All(char.IsDigit))
            throw new InvalidOperationException("任务年月格式必须为 yyyyMM，例如 202604。");
        if (record.TotalBudget <= 0)
            throw new InvalidOperationException("任务总预算必须大于 0。");
        if (record.FloatRate < 0 || record.FloatRate > 1)
            throw new InvalidOperationException("预算浮动率必须在 0 到 1 之间。");
    }
}

public sealed class TaskRecord
{
    public int Id { get; set; }
    public string YearMonth { get; set; } = string.Empty;
    public decimal TotalBudget { get; set; }
    public decimal FloatRate { get; set; }
    public int Status { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CategoryBudgetRecord
{
    public string Code { get; set; } = string.Empty;
    public decimal Ratio { get; set; }
    public int FrequencyDays { get; set; }
    public int DailyMinItems { get; set; } = 1;
    public int DailyMaxItems { get; set; } = 1;
}

public sealed class ProductPriceRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
}

public sealed class ProcurementDetailRecord
{
    public int TaskId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}
