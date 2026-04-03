using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenProcurement.Core.Entities
{
    /// <summary>
    /// 商品分类实体
    /// </summary>
    [Table("categories")]
    public class Category
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 分类名称
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Name { get; set; }

        /// <summary>
        /// 分类编码
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Code { get; set; }

        /// <summary>
        /// 预算占比（如0.45表示45%）
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(5,4)")]
        public decimal Ratio { get; set; }

        /// <summary>
        /// 出现频率（几天一次）
        /// </summary>
        [Required]
        public int FrequencyDays { get; set; } = 1;

        /// <summary>
        /// 每日最小采购品类数
        /// </summary>
        [Required]
        [Column("daily_min_items")]
        public int DailyMinItems { get; set; } = 1;

        /// <summary>
        /// 每日最大采购品类数
        /// </summary>
        [Required]
        [Column("daily_max_items")]
        public int DailyMaxItems { get; set; } = 1;

        /// <summary>
        /// 排序号
        /// </summary>
        [Required]
        public int Sort { get; set; } = 0;


        /// <summary>
        /// 状态（1启用，0禁用）
        /// </summary>
        [Required]
        public bool Status { get; set; } = true;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}