using System;
using System.Collections.Generic;

namespace CanteenProcurement.Application.DTOs
{
    /// <summary>
    /// 采购任务数据传输对象
    /// </summary>
    public class ProcurementTaskDto
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 年月
        /// </summary>
        public string YearMonth { get; set; } = string.Empty;


        /// <summary>
        /// 总预算
        /// </summary>
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// 随机波动率
        /// </summary>
        public decimal FloatRate { get; set; }

        /// <summary>
        /// 状态（0待生成，1已完成，2已取消）
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 状态描述
        /// </summary>
        public string StatusDescription { get; set; }

        /// <summary>
        /// 生成时间
        /// </summary>
        public DateTime? GeneratedAt { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; }

        /// <summary>
        /// 开始日期
        /// </summary>
        public DateTime StartDate => GetStartDate();

        /// <summary>
        /// 结束日期
        /// </summary>
        public DateTime EndDate => GetEndDate();

        private DateTime GetStartDate()
        {
            if (string.IsNullOrEmpty(YearMonth) || YearMonth.Length != 6)
                return DateTime.MinValue;

            var year = int.Parse(YearMonth.Substring(0, 4));
            var month = int.Parse(YearMonth.Substring(4, 2));
            return new DateTime(year, month, 1);
        }

        private DateTime GetEndDate()
        {
            var startDate = GetStartDate();
            return startDate.AddMonths(1).AddDays(-1);
        }
    }

    /// <summary>
    /// 创建任务数据传输对象
    /// </summary>
    public class CreateTaskDto
    {
        /// <summary>
        /// 年月
        /// </summary>
        public string YearMonth { get; set; } = string.Empty;


        /// <summary>
        /// 总预算
        /// </summary>
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// 随机波动率
        /// </summary>
        public decimal FloatRate { get; set; }

        /// <summary>
        /// 分类固定金额配置（可选）
        /// Key: 分类编码, Value: 固定金额
        /// </summary>
        public Dictionary<string, decimal>? CategoryFixedAmounts { get; set; }
    }

    /// <summary>
    /// 更新任务数据传输对象
    /// </summary>
    public class UpdateTaskDto
    {
        /// <summary>
        /// 总预算
        /// </summary>
        public decimal? TotalBudget { get; set; }

        /// <summary>
        /// 随机波动率
        /// </summary>
        public decimal? FloatRate { get; set; }
    }

    /// <summary>
    /// 生成计划结果数据传输对象
    /// </summary>
    public class GeneratePlanResultDto
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 生成的采购明细数量
        /// </summary>
        public int GeneratedItemsCount { get; set; }

        /// <summary>
        /// 实际使用总金额
        /// </summary>
        public decimal ActualTotalAmount { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }
    }

    /// <summary>
    /// 任务状态数据传输对象
    /// </summary>
    public class TaskStatusDto
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 状态代码
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 状态描述
        /// </summary>
        public string StatusDescription { get; set; }

        /// <summary>
        /// 是否可编辑
        /// </summary>
        public bool CanEdit { get; set; }

        /// <summary>
        /// 是否可生成
        /// </summary>
        public bool CanGenerate { get; set; }

        /// <summary>
        /// 是否可删除
        /// </summary>
        public bool CanDelete { get; set; }
    }

    /// <summary>
    /// 任务预算详情数据传输对象
    /// </summary>
    public class TaskBudgetDetailDto
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 总预算
        /// </summary>
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// 已使用预算
        /// </summary>
        public decimal UsedBudget { get; set; }

        /// <summary>
        /// 剩余预算
        /// </summary>
        public decimal RemainingBudget { get; set; }

        /// <summary>
        /// 分类预算详情
        /// </summary>
        public List<CategoryBudgetDetailDto> CategoryBudgets { get; set; } = new List<CategoryBudgetDetailDto>();
    }

    /// <summary>
    /// 分类预算详情数据传输对象
    /// </summary>
    public class CategoryBudgetDetailDto
    {
        /// <summary>
        /// 分类编码
        /// </summary>
        public string CategoryCode { get; set; }

        /// <summary>
        /// 分类名称
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 预算占比
        /// </summary>
        public decimal Ratio { get; set; }

        /// <summary>
        /// 分配预算
        /// </summary>
        public decimal Budget { get; set; }

        /// <summary>
        /// 已使用预算
        /// </summary>
        public decimal UsedBudget { get; set; }

        /// <summary>
        /// 使用率
        /// </summary>
        public decimal UsageRate { get; set; }
    }

    /// <summary>
    /// 任务统计数据传输对象
    /// </summary>
    public class TaskStatitisticsDto
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 采购明细总数
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// 涉及商品总数
        /// </summary>
        public int TotalProducts { get; set; }

        /// <summary>
        /// 涉及分类总数
        /// </summary>
        public int TotalCategories { get; set; }

        /// <summary>
        /// 实际总金额
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// 平均每日金额
        /// </summary>
        public decimal AverageDailyAmount { get; set; }

        /// <summary>
        /// 执行成功率
        /// </summary>
        public decimal ExecutionRate { get; set; }

        /// <summary>
        /// 分类统计详情
        /// </summary>
        public List<CategoryStatsItemDto> CategoryStats { get; set; } = new List<CategoryStatsItemDto>();
    }

    /// <summary>
    /// 分类统计项数据传输对象
    /// </summary>
    public class CategoryStatsItemDto
    {
        /// <summary>
        /// 分类编码
        /// </summary>
        public string CategoryCode { get; set; }

        /// <summary>
        /// 分类名称
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 涉及商品数
        /// </summary>
        public int ProductCount { get; set; }

        /// <summary>
        /// 采购明细数
        /// </summary>
        public int ItemCount { get; set; }

        /// <summary>
        /// 总金额
        /// </summary>
        public decimal TotalAmount { get; set; }

        /// <summary>
        /// 平均金额
        /// </summary>
        public decimal AverageAmount { get; set; }
    }

    /// <summary>
    /// 导出结果数据传输对象
    /// </summary>
    public class ExportResultDto
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 导出记录数
        /// </summary>
        public int RecordCount { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// 分类预算配置数据传输对象
    /// </summary>
    public class CategoryBudgetConfigDto
    {
        /// <summary>
        /// 分类编码
        /// </summary>
        public string CategoryCode { get; set; }

        /// <summary>
        /// 分类名称
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        /// 预算占比
        /// </summary>
        public decimal Ratio { get; set; }

        /// <summary>
        /// 是否启用固定金额模式
        /// </summary>
        public bool IsFixedAmount { get; set; } = false;

        /// <summary>
        /// 固定金额值（IsFixedAmount=true时必填）
        /// </summary>
        public decimal? FixedAmount { get; set; }
    }
}