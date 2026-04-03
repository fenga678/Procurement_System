# MySQL 到 SQLite 渐进式迁移规则

## 概述

本文档定义了食堂采购管理系统从 MySQL 迁移到 SQLite 的规则和实施步骤。采用渐进式迁移方案，系统将同时支持 MySQL 和 SQLite 两种数据库，默认使用 SQLite。

## 迁移目标

1. 系统同时支持 MySQL 和 SQLite 数据库
2. 用户可在系统设置中切换数据库类型
3. 默认使用 SQLite（零配置启动）
4. 保持现有功能完全兼容

## 架构设计

### 数据库抽象层

```
┌─────────────────────────────────────────────────────────┐
│                    Application Layer                     │
│  (Views, ViewModels, Services)                          │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                 Database Provider Layer                  │
│  ┌─────────────────┐    ┌─────────────────┐            │
│  │ IDatabaseProvider│◄───│DatabaseProviderFactory        │
│  └─────────────────┘    └─────────────────┘            │
│           ▲                        ▲                    │
│           │                        │                    │
│  ┌────────┴────────┐    ┌────────┴────────┐            │
│  │MySqlProvider    │    │SqliteProvider   │            │
│  └─────────────────┘    └─────────────────┘            │
└─────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────┐
│                 Database Connection                      │
│  ┌─────────────────┐    ┌─────────────────┐            │
│  │ MySqlConnection  │    │SqliteConnection │            │
│  └─────────────────┘    └─────────────────┘            │
└─────────────────────────────────────────────────────────┘
```

### 核心接口定义

```csharp
/// <summary>
/// 数据库提供者接口 - 抽象不同数据库的差异
/// </summary>
public interface IDatabaseProvider
{
    /// <summary>数据库名称标识</summary>
    string Name { get; }
    
    /// <summary>创建数据库连接</summary>
    Task<DbConnection> CreateConnectionAsync();
    
    /// <summary>获取最后插入ID的SQL</summary>
    string GetLastInsertIdSql();
    
    /// <summary>获取当前时间戳SQL</summary>
    string GetCurrentTimestampSql();
    
    /// <summary>获取当前日期SQL</summary>
    string GetCurrentDateSql();
    
    /// <summary>字符串拼接</summary>
    string Concat(params string[] parts);
    
    /// <summary>LIKE 查询的字符串拼接</summary>
    string LikeConcat(string column, string parameter);
    
    /// <summary>检查表结构是否存在某列</summary>
    Task<bool> HasColumnAsync(DbConnection conn, string tableName, string columnName);
    
    /// <summary>连接后初始化（如设置字符集、外键等）</summary>
    Task InitializeConnectionAsync(DbConnection conn);
}
```

## SQL 语法映射规则

### 函数映射表

| 功能 | MySQL | SQLite |
|------|-------|--------|
| 当前时间 | `NOW()` | `datetime('now', 'localtime')` |
| 当前日期 | `CURDATE()` | `date('now', 'localtime')` |
| 最后插入ID | `LAST_INSERT_ID()` | `last_insert_rowid()` |
| 字符串拼接 | `CONCAT(a, b, c)` | `a || b || c` |
| LIKE 拼接 | `LIKE CONCAT('%', @kw, '%')` | `LIKE '%' || @kw || '%'` |

### 数据类型映射表

| MySQL | SQLite | 说明 |
|-------|--------|------|
| `INT` | `INTEGER` | 整数 |
| `BIGINT` | `INTEGER` | 长整数 |
| `TINYINT(1)` | `INTEGER` | 布尔值 (0/1) |
| `VARCHAR(n)` | `TEXT` | 可变字符串 |
| `CHAR(n)` | `TEXT` | 定长字符串 |
| `DECIMAL(m,n)` | `REAL` 或 `TEXT` | 精确小数 |
| `TEXT` | `TEXT` | 长文本 |
| `DATETIME` | `TEXT` | 日期时间 (ISO8601格式) |
| `DATE` | `TEXT` | 日期 (ISO8601格式) |

### DDL 差异处理

#### 主键自增

```sql
-- MySQL
id INT PRIMARY KEY AUTO_INCREMENT

-- SQLite
id INTEGER PRIMARY KEY AUTOINCREMENT
```

#### 默认值

```sql
-- MySQL
created_at DATETIME DEFAULT CURRENT_TIMESTAMP

-- SQLite
created_at TEXT DEFAULT (datetime('now', 'localtime'))
```

#### 自动更新时间 (SQLite 需用触发器)

```sql
-- MySQL
updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP

-- SQLite 需要触发器
CREATE TRIGGER update_timestamp 
AFTER UPDATE ON table_name
BEGIN
    UPDATE table_name SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;
```

## 文件修改清单

### 新增文件

| 文件路径 | 说明 |
|----------|------|
| `src/CanteenProcurement.Core/Interfaces/IDatabaseProvider.cs` | 数据库提供者接口 |
| `src/CanteenProcurement.Wpf/Providers/MySqlProvider.cs` | MySQL 实现 |
| `src/CanteenProcurement.Wpf/Providers/SqliteProvider.cs` | SQLite 实现 |
| `src/CanteenProcurement.Wpf/Providers/DatabaseProviderFactory.cs` | 提供者工厂 |
| `database/03_create_database_sqlite.sql` | SQLite 建表脚本 |

### 修改文件

| 文件路径 | 修改内容 |
|----------|----------|
| `src/CanteenProcurement.Wpf/CanteenProcurement.Wpf.csproj` | 添加 SQLite NuGet 包 |
| `src/CanteenProcurement.Wpf/appsettings.json` | 添加数据库类型配置 |
| `src/CanteenProcurement.Wpf/Services/DatabaseConfig.cs` | 重构为支持双数据库 |
| `src/CanteenProcurement.Wpf/Services/SchemaCapabilitiesProvider.cs` | 适配双数据库 |
| `src/CanteenProcurement.Wpf/Services/TaskDataService.cs` | SQL 语法适配 |
| `src/CanteenProcurement.Wpf/Services/ProductDataService.cs` | SQL 语法适配 |
| `src/CanteenProcurement.Wpf/Services/CategoryDataService.cs` | SQL 语法适配 |
| `src/CanteenProcurement.Wpf/Views/SystemSettingsView.xaml` | 添加数据库选项 UI |
| `src/CanteenProcurement.Wpf/Views/SystemSettingsView.xaml.cs` | 数据库切换逻辑 |

## 配置文件格式

### appsettings.json

```json
{
  "Database": {
    "Provider": "Sqlite",
    "Sqlite": {
      "DatabasePath": "data/canteen.db"
    },
    "MySql": {
      "Server": "localhost",
      "Port": 3306,
      "Database": "canteen_procurement",
      "User": "root",
      "Password": ""
    }
  }
}
```

## 实施步骤

### 阶段一：基础设施（预计 2 天）

1. ✅ 创建迁移规则文件
2. 添加 SQLite NuGet 包
3. 创建 IDatabaseProvider 接口
4. 实现 MySqlProvider
5. 实现 SqliteProvider
6. 创建 DatabaseProviderFactory
7. 创建 SQLite 建表脚本

### 阶段二：服务层迁移（预计 3 天）

1. 重构 DatabaseConfig.cs
2. 修改 SchemaCapabilitiesProvider.cs
3. 修改 TaskDataService.cs
4. 修改 ProductDataService.cs
5. 修改 CategoryDataService.cs

### 阶段三：UI 层更新（预计 1 天）

1. 修改 SystemSettingsView.xaml
2. 修改 SystemSettingsView.xaml.cs
3. 更新配置文件

### 阶段四：测试验证（预计 2 天）

1. SQLite 模式功能测试
2. MySQL 模式功能测试
3. 数据库切换测试
4. 性能测试

## 注意事项

### 关键约束

1. **外键约束**：SQLite 默认不启用外键，需要在连接时执行 `PRAGMA foreign_keys = ON;`
2. **并发写入**：SQLite 写操作会锁定整个数据库文件，适合读多写少场景
3. **事务隔离**：SQLite 默认使用 SERIALIZABLE 隔离级别
4. **日期格式**：SQLite 存储日期为 TEXT，格式必须为 ISO8601 (`YYYY-MM-DD HH:MM:SS`)

### 兼容性处理

1. 所有 SQL 使用参数化查询，避免拼接
2. 时间相关字段在 C# 层格式化
3. 布尔值使用 `INTEGER` 存储 0/1
4. `updated_at` 字段在更新时显式设置

### 数据迁移

如果需要从现有 MySQL 迁移数据到 SQLite：

1. 导出 MySQL 数据为 SQL 或 CSV
2. 转换数据类型和日期格式
3. 导入到 SQLite
4. 验证数据完整性

## 回滚方案

如果迁移出现问题：

1. 修改 `appsettings.json` 中 `Database.Provider` 为 `MySql`
2. 确保 MySQL 服务可用
3. 重新启动应用程序

## 验收标准

1. ✅ 新安装用户可使用 SQLite 零配置启动
2. ✅ 系统设置可切换数据库类型
3. ✅ 所有 CRUD 功能正常工作
4. ✅ 数据完整性约束有效
5. ✅ 现有 MySQL 用户可继续使用

---

**文档版本**: 1.0  
**创建日期**: 2026-04-03  
**最后更新**: 2026-04-03
