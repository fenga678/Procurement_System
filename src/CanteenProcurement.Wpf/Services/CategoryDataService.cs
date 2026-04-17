using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Wpf.Services;

/// <summary>
/// 分类数据服务 - WPF 层用例服务，负责 DTO 转换和校验
/// </summary>
public sealed class CategoryDataService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryDataService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<List<CategoryRecord>> GetCategoriesAsync()
    {
        var categories = await _categoryRepository.GetAllAsync();
        return categories
            .Select(category => new CategoryRecord
            {
                Id = category.Id,
                Name = category.Name,
                Code = category.Code,
                Ratio = category.Ratio,
                FrequencyDays = category.FrequencyDays,
                Sort = category.Sort,
                Status = category.Status,
                UpdatedAt = category.UpdatedAt,
                DailyMinItems = category.DailyMinItems,
                DailyMaxItems = category.DailyMaxItems
            })
            .ToList();
    }

    public Task<int> CreateCategoryAsync(CategoryRecord record)
    {
        ValidateCategoryRecord(record, isNew: true);
        return _categoryRepository.CreateAsync(ToEntity(record));
    }

    public Task<int> UpdateCategoryAsync(CategoryRecord record)
    {
        ValidateCategoryRecord(record, isNew: false);
        return _categoryRepository.UpdateAsync(ToEntity(record));
    }

    public Task<int> UpdateCategoryStatusAsync(string code, bool status)
    {
        return _categoryRepository.UpdateStatusAsync(code, status);
    }

    public async Task<(bool Success, string? Message)> CanDeleteCategoryAsync(string code)
    {
        var result = await _categoryRepository.CanDeleteAsync(code);
        return (result.CanDelete, result.Message);
    }

    public Task<int> DeleteCategoryAsync(string code)
    {
        return _categoryRepository.DeleteAsync(code);
    }

    private static Category ToEntity(CategoryRecord record)
    {
        return new Category
        {
            Id = record.Id,
            Name = record.Name.Trim(),
            Code = record.Code.Trim(),
            Ratio = record.Ratio,
            FrequencyDays = record.FrequencyDays,
            DailyMinItems = record.DailyMinItems,
            DailyMaxItems = record.DailyMaxItems,
            Sort = record.Sort,
            Status = record.Status
        };
    }

    private static void ValidateCategoryRecord(CategoryRecord record, bool isNew)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        
        var name = record.Name?.Trim();
        var code = record.Code?.Trim();
        
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("分类名称不能为空。");
        if (name.Length > 50)
            throw new InvalidOperationException("分类名称不能超过 50 个字符。");
            
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("分类编码不能为空。");
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
            throw new InvalidOperationException("编码格式不正确。必须以字母开头，只能包含字母、数字和下划线。");
        if (code.Length > 20)
            throw new InvalidOperationException("编码不能超过 20 个字符。");
            
        if (record.Ratio < 0 || record.Ratio > 1)
            throw new InvalidOperationException("分类占比必须在 0 到 1 之间。");
        if (record.FrequencyDays <= 0)
            throw new InvalidOperationException("出现频率必须大于 0 天。");
        if (record.DailyMinItems <= 0 || record.DailyMaxItems <= 0)
            throw new InvalidOperationException("每日品类上下限必须大于 0。");
        if (record.DailyMinItems > record.DailyMaxItems)
            throw new InvalidOperationException("每日最小品类数不能大于最大品类数。");
    }
}

public sealed class CategoryRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Ratio { get; set; }
    public int FrequencyDays { get; set; }
    public int DailyMinItems { get; set; } = 1;
    public int DailyMaxItems { get; set; } = 1;
    public int Sort { get; set; }
    public bool Status { get; set; }
    public DateTime UpdatedAt { get; set; }
}
