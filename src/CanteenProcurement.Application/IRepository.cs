using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Application.Interfaces
{
    /// <summary>
    /// 通用仓储接口
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IRepository<T> where T : class
    {
        /// <summary>
        /// 根据ID获取实体
        /// </summary>
        Task<T> GetByIdAsync(int id);

        /// <summary>
        /// 获取所有实体
        /// </summary>
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary>
        /// 根据条件查询实体
        /// </summary>
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// 获取单个实体
        /// </summary>
        Task<T> SingleAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// 添加实体
        /// </summary>
        Task AddAsync(T entity);

        /// <summary>
        /// 添加多个实体
        /// </summary>
        Task AddManyAsync(IEnumerable<T> entities);

        /// <summary>
        /// 更新实体
        /// </summary>
        void Update(T entity);

        /// <summary>
        /// 删除实体
        /// </summary>
        void Delete(T entity);

        /// <summary>
        /// 判断实体是否存在
        /// </summary>
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// 获取符合条件的实体数量
        /// </summary>
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);

        /// <summary>
        /// 保存更改
        /// </summary>
        Task<bool> SaveChangesAsync();
    }
}