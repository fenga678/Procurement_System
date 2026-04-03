using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenProcurement.Core.Entities
{
    /// <summary>
    /// 采购任务状态枚举
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>
        /// 待生成
        /// </summary>
        Pending = 0,
        
        /// <summary>
        /// 已完成
        /// </summary>
        Completed = 1,
        
        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled = 2
    }

    /// <summary>
    /// 采购任务实体
    /// </summary>
    [Table("procurement_tasks")]
    public class ProcurementTask
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 年月（如202604）
        /// </summary>
        [Required]
        [StringLength(6)]
        public string YearMonth { get; set; }

        /// <summary>
        /// 月度总预算
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(12,2)")]
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// 随机波动率（如0.1表示±10%）
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(4,3)")]
        public decimal FloatRate { get; set; } = 0.100m;

        /// <summary>
        /// 任务状态
        /// </summary>
        [Required]
        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        /// <summary>
        /// 计划生成时间
        /// </summary>
        public DateTime? GeneratedAt { get; set; }

        /// <summary>
        /// 创建人
        /// </summary>
        [StringLength(50)]
        public string? CreatedBy { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 关联的分类预算
        /// </summary>
        public virtual ICollection<TaskCategoryBudget> CategoryBudgets { get; set; }

        /// <summary>
        /// 关联的采购明细
        /// </summary>
        public virtual ICollection<ProcurementDetail> ProcurementDetails { get; set; }

        /// <summary>
        /// 分类固定金额配置（可选）
        /// Key: 分类编码, Value: 固定金额
        /// 注意：此字段不映射到数据库，仅用于任务创建时传递配置
        /// </summary>
        [NotMapped]
        public Dictionary<string, decimal>? CategoryFixedAmounts { get; set; }

        /// <summary>
        /// 获取任务对应的开始日期
        /// </summary>
        public DateTime GetStartDate()
        {
            var year = int.Parse(YearMonth.Substring(0, 4));
            var month = int.Parse(YearMonth.Substring(4, 2));
            return new DateTime(year, month, 1);
        }

        /// <summary>
        /// 获取任务对应的结束日期
        /// </summary>
        public DateTime GetEndDate()
        {
            var startDate = GetStartDate();
            return startDate.AddMonths(1).AddDays(-1);
        }

        /// <summary>
        /// 获取任务月份的天数
        /// </summary>
        public int GetDaysInMonth()
        {
            var startDate = GetStartDate();
            return DateTime.DaysInMonth(startDate.Year, startDate.Month);
        }
    }
}