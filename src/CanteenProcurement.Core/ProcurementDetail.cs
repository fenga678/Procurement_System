using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenProcurement.Core.Entities
{
    /// <summary>
    /// 采购明细实体
    /// </summary>
    [Table("procurement_details")]
    public class ProcurementDetail
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

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
        /// 商品ID
        /// </summary>
        [Required]
        public int ProductId { get; set; }

        /// <summary>
        /// 商品关系
        /// </summary>
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        /// <summary>
        /// 采购日期
        /// </summary>
        [Required]
        public DateTime PurchaseDate { get; set; }

        /// <summary>
        /// 单价
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(10,2)")]
        public decimal Price { get; set; }

        /// <summary>
        /// 采购数量
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(10,2)")]
        public decimal Quantity { get; set; }

        /// <summary>
        /// 采购金额
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(12,2)")]
        public decimal Amount { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}