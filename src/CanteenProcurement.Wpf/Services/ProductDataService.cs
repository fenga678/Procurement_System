using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Wpf.Services;

/// <summary>
/// 商品数据服务 - WPF 层用例服务
/// </summary>
public sealed class ProductDataService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public ProductDataService(IProductRepository productRepository, ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
    }

    public async Task<List<ProductRecord>> GetProductsAsync(string? categoryCode = null, string? keyword = null)
    {
        var products = await _productRepository.GetAllAsync(categoryCode, keyword);
        var categories = await _categoryRepository.GetAllAsync();
        var categoryLookup = categories.ToDictionary(c => c.Code, c => c.Name, StringComparer.OrdinalIgnoreCase);

        return products.Select(product => new ProductRecord
        {
            Id = product.Id,
            Name = product.Name,
            CategoryCode = product.CategoryCode,
            CategoryName = categoryLookup.TryGetValue(product.CategoryCode, out var name) ? name : product.CategoryCode,
            Price = product.Price,
            Unit = product.Unit,
            MinIntervalDays = product.MinIntervalDays,
            IsActive = product.IsActive,
            Remark = product.Remark ?? string.Empty,
            UpdatedAt = product.UpdatedAt
        }).ToList();
    }

    public Task<int> CreateProductAsync(ProductRecord record)
    {
        ValidateProductRecord(record);
        return _productRepository.CreateAsync(ToEntity(record));
    }

    public Task<int> UpdateProductAsync(ProductRecord record)
    {
        ValidateProductRecord(record);
        return _productRepository.UpdateAsync(ToEntity(record));
    }

    public Task<int> UpdateProductStatusAsync(int id, bool status)
    {
        return _productRepository.UpdateStatusAsync(id, status);
    }

    public async Task<(bool Success, string? Message)> CanDeleteProductAsync(int id)
    {
        var result = await _productRepository.CanDeleteAsync(id);
        return (result.CanDelete, result.Message);
    }

    public Task<int> DeleteProductAsync(int id)
    {
        return _productRepository.DeleteAsync(id);
    }

    private static Product ToEntity(ProductRecord record)
    {
        return new Product
        {
            Id = record.Id,
            Name = record.Name.Trim(),
            CategoryCode = record.CategoryCode.Trim(),
            Price = record.Price,
            Unit = record.Unit.Trim(),
            MinIntervalDays = record.MinIntervalDays,
            IsActive = record.IsActive,
            Remark = record.Remark?.Trim()
        };
    }

    private static void ValidateProductRecord(ProductRecord record)
    {
        if (record == null) throw new ArgumentNullException(nameof(record));
        if (string.IsNullOrWhiteSpace(record.Name)) throw new InvalidOperationException("商品名称不能为空。");
        if (string.IsNullOrWhiteSpace(record.CategoryCode)) throw new InvalidOperationException("商品分类不能为空。");
        if (string.IsNullOrWhiteSpace(record.Unit)) throw new InvalidOperationException("商品单位不能为空。");
        if (record.MinIntervalDays <= 0) throw new InvalidOperationException("最小间隔天数必须大于 0。");
        if (record.Price <= 0) throw new InvalidOperationException("商品单价必须大于 0。");
    }
}

public sealed class ProductRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int MinIntervalDays { get; set; }
    public bool IsActive { get; set; }
    public string Remark { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
