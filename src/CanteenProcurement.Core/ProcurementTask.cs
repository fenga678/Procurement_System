using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace CanteenProcurement.Core.Entities
{
    public enum TaskStatus
    {
        Pending = 0,
        Completed = 1,
        Cancelled = 2
    }

    [Table("procurement_tasks")]
    public class ProcurementTask
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 年月（格式：yyyyMM，如 202604）
        /// </summary>
        [Required]
        [StringLength(6)]
        [RegularExpression(@"^\d{6}$")]
        [Column("year_m")]
        public string YearMonth { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "DECIMAL(12,2)")]
        [Range(typeof(decimal), "0.01", "999999999999")]
        public decimal TotalBudget { get; set; }

        [Required]
        [Column(TypeName = "DECIMAL(4,3)")]
        [Range(typeof(decimal), "0", "1")]
        public decimal FloatRate { get; set; } = 0.100m;

        [Required]
        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        public DateTime? GeneratedAt { get; set; }

        [StringLength(50)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<TaskCategoryBudget> CategoryBudgets { get; set; } = new List<TaskCategoryBudget>();

        public virtual ICollection<ProcurementDetail> ProcurementDetails { get; set; } = new List<ProcurementDetail>();

        [NotMapped]
        public Dictionary<string, decimal>? CategoryFixedAmounts { get; set; }

        public DateTime GetStartDate()
        {
            if (!DateTime.TryParseExact(YearMonth, "yyyyMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                throw new InvalidOperationException($"YearMonth 格式无效：{YearMonth}。正确格式应为 yyyyMM，例如 202604。");
            }

            return new DateTime(parsedDate.Year, parsedDate.Month, 1);
        }

        public DateTime GetEndDate() => GetStartDate().AddMonths(1).AddDays(-1);

        public int GetDaysInMonth()
        {
            var startDate = GetStartDate();
            return DateTime.DaysInMonth(startDate.Year, startDate.Month);
        }
    }
}
