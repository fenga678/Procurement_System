using System;
using System.Threading.Tasks;

namespace CanteenProcurement.Core.Algorithms
{
    /// <summary>
    /// 采购算法引擎接口
    /// </summary>
    public interface IProcurementAlgorithmEngine
    {
        /// <summary>
        /// 执行采购计划生成
        /// </summary>
        /// <param name="task">采购任务</param>
        /// <returns>调度结果</returns>
        Task<ScheduleResult> ExecuteAsync(Entities.ProcurementTask task);
    }

    /// <summary>
    /// 分类调度策略接口
    /// </summary>
    public interface ICategorySchedulingStrategy
    {
        /// <summary>
        /// 执行分类调度
        /// </summary>
        /// <param name="categoryBudget">分类预算</param>
        /// <param name="products">商品列表</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <returns>分类调度结果</returns>
        Task<CategorySchedule> ScheduleAsync(
            Entities.TaskCategoryBudget categoryBudget,
            System.Collections.Generic.List<Entities.Product> products,
            DateTime startDate,
            DateTime endDate);
    }

    /// <summary>
    /// 规则引擎接口
    /// </summary>
    public interface IScheduleRule
    {
        /// <summary>
        /// 规则名称
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// 验证规则
        /// </summary>
        /// <param name="item">调度项</param>
        /// <param name="context">调度上下文</param>
        /// <returns>验证结果</returns>
        RuleValidationResult Validate(ScheduleItem item, ScheduleContext context);
    }
}