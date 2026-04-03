using System;

namespace CanteenProcurement.Application.DTOs
{
    /// <summary>
    /// 商品数据传输对象
    /// </summary>
    public class ProductDto
    {
        /// <summary>
        /// 商品ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 商品名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 分类编码
        /// </summary>
        public string CategoryCode { get; set; }

        /// <summary>
        /// 分类名称
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 单价
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 最小间隔天数
        /// </summary>
        public int MinIntervalDays { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

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
    /// 创建商品数据传输对象
    /// </summary>
    public class CreateProductDto
    {
        /// <summary>
        /// 商品名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 分类编码
        /// </summary>
        public string CategoryCode { get; set; }

        /// <summary>
        /// 单价
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 最小间隔天数
        /// </summary>
        public int MinIntervalDays { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }
    }

    /// <summary>
    /// 更新商品数据传输对象
    /// </summary>
    public class UpdateProductDto
    {
        /// <summary>
        /// 商品名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 单价
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 最小间隔天数
        /// </summary>
        public int MinIntervalDays { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool? IsActive { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }
    }

    /// <summary>
    /// 商品导入数据传输对象
    /// </summary>
    public class ProductImportDto
    {
        /// <summary>
        /// 商品名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 分类编码
        /// </summary>
        public string CategoryCode { get; set; }

        /// <summary>
        /// 单价
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 最小间隔天数
        /// </summary>
        public int MinIntervalDays { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }
    }

    /// <summary>
    /// 商品使用统计数据传输对象
    /// </summary>
    public class ProductUsageStatsDto
    {
        /// <summary>
        /// 商品ID
        /// </summary>
        public int ProductId { get; set; }

        /// <summary>
        /// 商品名称
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// 使用次数
        /// </summary>
        public int UsageCount { get; set; }

        /// <summary>
        /// 总采购金额
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// 平均采购金额
        /// </summary>
        public decimal AverageAmount { get; set; }

        /// <summary>
        /// 首次使用日期
        /// </summary>
        public DateTime? FirstUsedDate { get; set; }

        /// <summary>
        /// 最后使用日期
        /// </summary>
        public DateTime? LastUsedDate { get; set; }

        /// <summary>
        /// 平均使用间隔（天）
        /// </summary>
        public double AverageIntervalDays { get; set; }
    }

    /// <summary>
    /// 批量导入结果数据传输对象
    /// </summary>
    public class BatchImportResultDto
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 导入总数
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 成功数量
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// 失败数量
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public List<string> ErrorMessages { get; set; } = new List<string>();

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }
    }
}