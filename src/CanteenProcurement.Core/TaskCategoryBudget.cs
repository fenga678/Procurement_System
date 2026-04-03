using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenProcurement.Core.Entities
{
    /// <summary>
    /// 任务分类预算实体
    /// </summary>
    [Table("task_category_budgets")]
    public class TaskCategoryBudget
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 任务ID
        /// </summary>
        [Required]
        public int TaskId { get; set; }

        /// <summary>
        /// 任务关系
        /// </summary>
        [ForeignKey("TaskId")]
        public virtual ProcurementTask Task { get; set; }

        /// <summary>
        /// 分类编码
        /// </summary>
        [Required]
        [StringLength(20)]
        public string CategoryCode { get; set; }

        /// <summary>
        /// 分类关系
        /// </summary>
        [ForeignKey("CategoryCode")]
        public virtual Category Category { get; set; }

        /// <summary>
        /// 预算占比
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(5,4)")]
        public decimal Ratio { get; set; }

        /// <summary>
        /// 分类预算金额
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(12,2)")]
        public decimal Budget { get; set; }

        /// <summary>
        /// 预计商品种类数
        /// </summary>
        [Required]
        public int ExpectedCount { get; set; } = 1;

        /// <summary>
        /// 实际使用商品数
        /// </summary>
        public int ActualCount { get; set; } = 0;

        /// <summary>
        /// 是否为固定金额（true=使用FixedAmount，false=使用Ratio计算）
        /// </summary>
        [Required]
        public bool IsFixedAmount { get; set; } = false;

        /// <summary>
        /// 固定金额（IsFixedAmount=true时生效）
        /// </summary>
        [Column(TypeName = "DECIMAL(12,2)")]
        public decimal? FixedAmount { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}