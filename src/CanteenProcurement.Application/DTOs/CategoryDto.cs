using System;

namespace CanteenProcurement.Application.DTOs
{
    /// <summary>
    /// 分类数据传输对象
    /// </summary>
    public class CategoryDto
    {
        /// <summary>
        /// 分类ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 分类名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 分类编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 预算占比
        /// </summary>
        public decimal Ratio { get; set; }

        /// <summary>
        /// 出现频率（天）
        /// </summary>
        public int FrequencyDays { get; set; }

        /// <summary>
        /// 每日最小采购品类数
        /// </summary>
        public int DailyMinItems { get; set; }

        /// <summary>
        /// 每日最大采购品类数
        /// </summary>
        public int DailyMaxItems { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int Sort { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        public bool Status { get; set; }


        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 创建分类数据传输对象
    /// </summary>
    public class CreateCategoryDto
    {
        /// <summary>
        /// 分类名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 分类编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 预算占比
        /// </summary>
        public decimal Ratio { get; set; }

        /// <summary>
        /// 出现频率（天）
        /// </summary>
        public int FrequencyDays { get; set; }

        /// <summary>
        /// 每日最小采购品类数
        /// </summary>
        public int DailyMinItems { get; set; }

        /// <summary>
        /// 每日最大采购品类数
        /// </summary>
        public int DailyMaxItems { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int Sort { get; set; }
    }


    /// <summary>
    /// 更新分类数据传输对象
    /// </summary>
    public class UpdateCategoryDto
    {
        /// <summary>
        /// 分类名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 预算占比
        /// </summary>
        public decimal Ratio { get; set; }

        /// <summary>
        /// 出现频率（天）
        /// </summary>
        public int FrequencyDays { get; set; }

        /// <summary>
        /// 每日最小采购品类数
        /// </summary>
        public int DailyMinItems { get; set; }

        /// <summary>
        /// 每日最大采购品类数
        /// </summary>
        public int DailyMaxItems { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int Sort { get; set; }

        /// <summary>
        /// 状态
        /// </summary>
        public bool? Status { get; set; }
    }


    /// <summary>
    /// 分类统计数据传输对象
    /// </summary>
    public class CategoryStatsDto
    {
        /// <summary>
        /// 总分类数
        /// </summary>
        public int TotalCategories { get; set; }

        /// <summary>
        /// 启用分类数
        /// </summary>
        public int ActiveCategories { get; set; }

        /// <summary>
        /// 预算比例汇总
        /// </summary>
        public decimal TotalRatio { get; set; }

        /// <summary>
        /// 按频次统计
        /// </summary>
        public Dictionary<int, int> FrequencyStats { get; set; } = new Dictionary<int, int>();
    }
}