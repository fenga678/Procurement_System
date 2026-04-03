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
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string CategoryCode { get; set; } = string.Empty;

        [ForeignKey("CategoryCode")]
        public virtual Category? Category { get; set; }

        [Required]
        [Column(TypeName = "DECIMAL(10,2)")]
        [Range(typeof(decimal), "0.01", "99999999")]
        public decimal Price { get; set; }

        [Required]
        [StringLength(20)]
        public string Unit { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int MinIntervalDays { get; set; } = 2;

        [Required]
        public bool IsActive { get; set; } = true;

        public string? Remark { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
