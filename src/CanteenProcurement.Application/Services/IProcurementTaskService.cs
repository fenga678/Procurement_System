using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CanteenProcurement.Application.DTOs;
using CanteenProcurement.Core.Algorithms;

namespace CanteenProcurement.Application.Services
{
    /// <summary>
    /// 采购任务服务接口
    /// </summary>
    public interface IProcurementTaskService
    {
        /// <summary>
        /// 创建采购任务
        /// </summary>
        Task<ProcurementTaskDto> CreateTaskAsync(CreateTaskDto dto);

        /// <summary>
        /// 获取任务列表
        /// </summary>
        Task<List<ProcurementTaskDto>> GetAllTasksAsync();

        /// <summary>
        /// 根据ID获取任务详情
        /// </summary>
        Task<ProcurementTaskDto> GetTaskByIdAsync(int taskId);

        /// <summary>
        /// 根据年月获取任务
        /// </summary>
        Task<ProcurementTaskDto> GetTaskByYearMonthAsync(string yearMonth);

        /// <summary>
        /// 生成采购计划
        /// </summary>
        Task<GeneratePlanResultDto> GenerateProcurementPlanAsync(int taskId, string userId = null);

        /// <summary>
        /// 更新任务信息
        /// </summary>
        Task<ProcurementTaskDto> UpdateTaskAsync(int taskId, UpdateTaskDto dto);

        /// <summary>
        /// 删除任务
        /// </summary>
        Task<bool> DeleteTaskAsync(int taskId);

        /// <summary>
        /// 获取任务状态
        /// </summary>
        Task<TaskStatusDto> GetTaskStatusAsync(int taskId);

        /// <summary>
        /// 更新任务状态
        /// </summary>
        Task<bool> UpdateTaskStatusAsync(int taskId, int status);

        /// <summary>
        /// 获取任务预算详情
        /// </summary>
        Task<TaskBudgetDetailDto> GetTaskBudgetDetailAsync(int taskId);

        /// <summary>
        /// 获取任务执行统计
        /// </summary>
        Task<TaskStatitisticsDto> GetTaskStatisticsAsync(int taskId);

        /// <summary>
        /// 取消任务生成
        /// </summary>
        Task<bool> CancelTaskGenerationAsync(int taskId);

        /// <summary>
        /// 导出任务数据
        /// </summary>
        Task<ExportResultDto> ExportTaskDataAsync(int taskId, string format = "excel");
    }
}