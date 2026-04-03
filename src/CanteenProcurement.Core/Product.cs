using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenProcurement.Core.Entities
{
    /// <summary>
    /// 商品实体
    /// </summary>
    [Table("products")]
    public class Product
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 商品名称
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

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
        /// 单价
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(10,2)")]
        public decimal Price { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        [Required]
        [StringLength(20)]
        public string Unit { get; set; }

        /// <summary>
        /// 最小间隔天数
        /// </summary>
        [Required]
        public int MinIntervalDays { get; set; } = 2;

        /// <summary>
        /// 是否启用
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 备注信息
        /// </summary>
        public string? Remark { get; set; }

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