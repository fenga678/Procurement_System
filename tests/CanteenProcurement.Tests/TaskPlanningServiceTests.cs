using CanteenProcurement.Application.DTOs;
using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Application.Services;
using CanteenProcurement.Core.Entities;
using FluentAssertions;
using Moq;
using TaskStatus = CanteenProcurement.Core.Entities.TaskStatus;
using Xunit;

namespace CanteenProcurement.Tests;

public class TaskPlanningServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepoMock;
    private readonly Mock<ICategoryRepository> _categoryRepoMock;
    private readonly Mock<IProductRepository> _productRepoMock;
    private readonly TaskPlanningService _service;

    public TaskPlanningServiceTests()
    {
        _taskRepoMock = new Mock<ITaskRepository>();
        _categoryRepoMock = new Mock<ICategoryRepository>();
        _productRepoMock = new Mock<IProductRepository>();
        _service = new TaskPlanningService(_taskRepoMock.Object, _categoryRepoMock.Object, _productRepoMock.Object);
    }

    [Fact]
    public async Task GenerateAsync_TaskNotFound_ReturnsFailure()
    {
        // Arrange
        _taskRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcurementTask?)null);

        // Act
        var result = await _service.GenerateAsync(999);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("未找到");
    }

    [Fact]
    public async Task GenerateAsync_NoActiveCategories_ReturnsFailure()
    {
        // Arrange
        var task = CreateTask(1, "202604", 10000m);
        _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _categoryRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Category>());
        _productRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Product>());

        // Act
        var result = await _service.GenerateAsync(1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("没有启用");
    }

    [Fact]
    public async Task GenerateAsync_NoProducts_ReturnsFailure()
    {
        // Arrange
        var task = CreateTask(1, "202604", 10000m);
        _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _categoryRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category> { CreateCategory("vegetable", 0.45m) });
        _productRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Product>());

        // Act
        var result = await _service.GenerateAsync(1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("没有可用商品");
    }

    [Fact]
    public async Task GenerateAsync_ValidData_ReturnsSuccess()
    {
        // Arrange
        var task = CreateTask(1, "202604", 10000m);
        var categories = new List<Category>
        {
            CreateCategory("vegetable", 0.45m, frequencyDays: 1, dailyMin: 1, dailyMax: 2),
            CreateCategory("meat", 0.55m, frequencyDays: 2, dailyMin: 1, dailyMax: 1)
        };
        var products = new List<Product>
        {
            CreateProduct(1, "白菜", "vegetable", 2.5m),
            CreateProduct(2, "五花肉", "meat", 28m)
        };

        _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _categoryRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);
        _productRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);
        _taskRepoMock.Setup(r => r.HasFixedAmountColumnsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _taskRepoMock.Setup(r => r.ReplaceDetailsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<ProcurementDetail>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _taskRepoMock.Setup(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<TaskStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.GenerateAsync(1);

        // Assert
        result.Success.Should().BeTrue();
        result.GeneratedItemsCount.Should().BeGreaterThan(0);
        result.ActualTotalAmount.Should().BeGreaterThan(0);

        // 验证替换明细和更新状态被调用
        _taskRepoMock.Verify(r => r.ReplaceDetailsAsync(1, It.IsAny<IReadOnlyList<ProcurementDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
        _taskRepoMock.Verify(r => r.UpdateStatusAsync(1, TaskStatus.Completed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_FixedAmountExceedsBudget_ThrowsException()
    {
        // Arrange
        var task = CreateTask(1, "202604", 1000m);
        var categories = new List<Category>
        {
            CreateCategory("vegetable", 0.5m),
            CreateCategory("meat", 0.5m)
        };
        var products = new List<Product>
        {
            CreateProduct(1, "白菜", "vegetable", 2.5m),
            CreateProduct(2, "五花肉", "meat", 28m)
        };
        var fixedAmounts = new Dictionary<string, decimal>
        {
            { "vegetable", 600m },
            { "meat", 500m } // 总额 1100 > 预算 1000
        };

        _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _categoryRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);
        _productRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);
        _taskRepoMock.Setup(r => r.HasFixedAmountColumnsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _taskRepoMock.Setup(r => r.GetFixedAmountsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(fixedAmounts);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GenerateAsync(1));
    }

    [Fact]
    public async Task GenerateAsync_CategoryWithoutProducts_ReturnsFailure()
    {
        // Arrange
        var task = CreateTask(1, "202604", 10000m);
        var categories = new List<Category>
        {
            CreateCategory("vegetable", 0.45m),
            CreateCategory("meat", 0.55m)
        };
        var products = new List<Product>
        {
            CreateProduct(1, "白菜", "vegetable", 2.5m)
            // meat 分类没有商品
        };

        _taskRepoMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(task);
        _categoryRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(categories);
        _productRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

        // Act
        var result = await _service.GenerateAsync(1);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("meat");
        result.Message.Should().Contain("没有可用商品");
    }

    [Fact]
    public void BudgetAllocation_VariableCategories_TotalEqualsBudget()
    {
        var categories = new List<Category>
        {
            CreateCategory("vegetable", 0.45m),
            CreateCategory("meat", 0.30m),
            CreateCategory("egg", 0.25m)
        };
        var totalBudget = 10000m;
        var fixedAmounts = new Dictionary<string, decimal>
        {
            { "egg", 1000m }
        };

        var plans = InvokeBuildCategoryBudgetPlans(totalBudget, categories, fixedAmounts);

        // 验证分配到了 3 个分类
        plans.Count.Should().Be(3);

        // 通过反射验证总额正确
        var totalAllocated = GetTotalBudgetFromPlans(plans);
        Math.Round(totalAllocated, 1).Should().Be(Math.Round(totalBudget, 1));
    }

    [Fact]
    public void BudgetAllocation_AllFixed_TotalEqualsSum()
    {
        var categories = new List<Category>
        {
            CreateCategory("vegetable", 0.5m),
            CreateCategory("meat", 0.5m)
        };
        var totalBudget = 5000m;
        var fixedAmounts = new Dictionary<string, decimal>
        {
            { "vegetable", 2000m },
            { "meat", 3000m }
        };

        var plans = InvokeBuildCategoryBudgetPlans(totalBudget, categories, fixedAmounts);

        plans.Count.Should().Be(2);
        var total = GetTotalBudgetFromPlans(plans);
        Math.Round(total, 1).Should().Be(Math.Round(totalBudget, 1));
    }

    [Fact]
    public void BudgetAllocation_FixedExceedsBudget_ThrowsInnerException()
    {
        var categories = new List<Category>
        {
            CreateCategory("vegetable", 1m)
        };
        var totalBudget = 1000m;
        var fixedAmounts = new Dictionary<string, decimal>
        {
            { "vegetable", 1500m }
        };

        var act = () => InvokeBuildCategoryBudgetPlans(totalBudget, categories, fixedAmounts);
        act.Should().Throw<System.Reflection.TargetInvocationException>()
            .WithInnerExceptionExactly<InvalidOperationException>()
            .WithMessage("*固定金额总额*超过任务预算*");
    }

    // ========== Helper Methods ==========

    private static ProcurementTask CreateTask(int id, string yearMonth, decimal totalBudget, decimal floatRate = 0.1m)
    {
        return new ProcurementTask
        {
            Id = id,
            YearMonth = yearMonth,
            TotalBudget = totalBudget,
            FloatRate = floatRate,
            Status = TaskStatus.Pending,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    private static Category CreateCategory(string code, decimal ratio, int frequencyDays = 1, int dailyMin = 1, int dailyMax = 1)
    {
        return new Category
        {
            Id = 1,
            Code = code,
            Name = code,
            Ratio = ratio,
            FrequencyDays = frequencyDays,
            DailyMinItems = dailyMin,
            DailyMaxItems = dailyMax,
            Sort = 0,
            Status = true,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    private static Product CreateProduct(int id, string name, string categoryCode, decimal price)
    {
        return new Product
        {
            Id = id,
            Name = name,
            CategoryCode = categoryCode,
            Price = price,
            Unit = "斤",
            IsActive = true,
            MinIntervalDays = 2,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    private static List<dynamic> InvokeBuildCategoryBudgetPlans(
        decimal totalBudget, List<Category> categories, Dictionary<string, decimal> fixedAmounts)
    {
        var method = typeof(TaskPlanningService).GetMethod(
            "BuildCategoryBudgetPlans",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (method == null)
            throw new InvalidOperationException("无法找到 BuildCategoryBudgetPlans 方法");

        var result = method.Invoke(null, new object[] { totalBudget, categories, fixedAmounts });
        var enumerable = result as System.Collections.IEnumerable ?? throw new InvalidOperationException("预算分配返回空值");
        var dynamicList = new List<dynamic>();
        foreach (var item in enumerable)
        {
            dynamicList.Add(item);
        }
        return dynamicList;
    }

    private static decimal GetTotalBudgetFromPlans(List<dynamic> plans)
    {
        decimal total = 0;
        foreach (var plan in plans)
        {
            var budgetProp = plan.GetType().GetProperty("Budget");
            if (budgetProp != null)
            {
                total += (decimal)budgetProp.GetValue(plan)!;
            }
        }
        return total;
    }
}
