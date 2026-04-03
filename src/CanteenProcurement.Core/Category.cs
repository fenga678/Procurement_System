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
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 预算占比（如0.45表示45%）
        /// </summary>
        [Required]
        [Column(TypeName = "DECIMAL(5,4)")]
        [Range(typeof(decimal), "0", "1")]
        public decimal Ratio { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int FrequencyDays { get; set; } = 1;

        [Required]
        [Column("daily_min_items")]
        [Range(1, int.MaxValue)]
        public int DailyMinItems { get; set; } = 1;

        [Required]
        [Column("daily_max_items")]
        [Range(1, int.MaxValue)]
        public int DailyMaxItems { get; set; } = 1;

        [Required]
        public int Sort { get; set; }

        [Required]
        public bool Status { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
