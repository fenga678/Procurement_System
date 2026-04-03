using System.Collections.Generic;
using System.Threading.Tasks;
using CanteenProcurement.Application.DTOs;

namespace CanteenProcurement.Application.Services
{
    /// <summary>
    /// 分类服务接口
    /// </summary>
    public interface ICategoryService
    {
        /// <summary>
        /// 获取所有分类
        /// </summary>
        Task<List<CategoryDto>> GetAllCategoriesAsync();

        /// <summary>
        /// 获取启用的分类
        /// </summary>
        Task<List<CategoryDto>> GetActiveCategoriesAsync();

        /// <summary>
        /// 根据编码获取分类
        /// </summary>
        Task<CategoryDto> GetCategoryByCodeAsync(string code);

        /// <summary>
        /// 创建分类
        /// </summary>
        Task<CategoryDto> CreateCategoryAsync(CreateCategoryDto dto);

        /// <summary>
        /// 更新分类
        /// </summary>
        Task<CategoryDto> UpdateCategoryAsync(string code, UpdateCategoryDto dto);

        /// <summary>
        /// 删除分类
        /// </summary>
        Task<bool> DeleteCategoryAsync(string code);

        /// <summary>
        /// 更新分类状态
        /// </summary>
        Task<bool> UpdateCategoryStatusAsync(string code, bool status);

        /// <summary>
        /// 获取分类统计信息
        /// </summary>
        Task<CategoryStatsDto> GetCategoryStatsAsync();
    }
}