using System.Collections.Generic;
using System.Threading.Tasks;
using CanteenProcurement.Application.DTOs;

namespace CanteenProcurement.Application.Services
{
    /// <summary>
    /// 商品服务接口
    /// </summary>
    public interface IProductService
    {
        /// <summary>
        /// 获取所有商品
        /// </summary>
        Task<List<ProductDto>> GetAllProductsAsync();

        /// <summary>
        /// 根据分类获取商品
        /// </summary>
        Task<List<ProductDto>> GetProductsByCategoryAsync(string categoryCode);

        /// <summary>
        /// 获取启用的商品
        /// </summary>
        Task<List<ProductDto>> GetActiveProductsAsync();

        /// <summary>
        /// 根据ID获取商品详情
        /// </summary>
        Task<ProductDto> GetProductByIdAsync(int id);

        /// <summary>
        /// 创建商品
        /// </summary>
        Task<ProductDto> CreateProductAsync(CreateProductDto dto);

        /// <summary>
        /// 更新商品
        /// </summary>
        Task<ProductDto> UpdateProductAsync(int id, UpdateProductDto dto);

        /// <summary>
        /// 删除商品
        /// </summary>
        Task<bool> DeleteProductAsync(int id);

        /// <summary>
        /// 更新商品状态
        /// </summary>
        Task<bool> UpdateProductStatusAsync(int id, bool status);

        /// <summary>
        /// 搜索商品
        /// </summary>
        Task<List<ProductDto>> SearchProductsAsync(string keyword);

        /// <summary>
        /// 获取商品使用统计
        /// </summary>
        Task<ProductUsageStatsDto> GetProductUsageStatsAsync(int productId);

        /// <summary>
        /// 批量导入商品
        /// </summary>
        Task<BatchImportResultDto> BatchImportProductsAsync(List<ProductImportDto> products);
    }
}