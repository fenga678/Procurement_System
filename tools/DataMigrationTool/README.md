# MySQL 到 SQLite 数据迁移工具

## 概述

此工具用于将食堂采购管理系统的数据从 MySQL 数据库迁移到 SQLite 数据库。

## 使用方法

### 方式一：交互式运行（推荐）

Windows:
```bash
cd tools/DataMigrationTool
run.bat
```

Linux/macOS:
```bash
cd tools/DataMigrationTool
chmod +x run.sh
./run.sh
```

然后按提示输入 MySQL 连接信息和 SQLite 数据库路径。

### 方式二：命令行参数

```bash
dotnet run --project DataMigrationTool.csproj -- "<mysql_connection_string>" "<sqlite_db_path>"
```

示例：
```bash
dotnet run --project DataMigrationTool.csproj -- "server=localhost;port=3306;database=canteen_procurement;user=root;password=123456;CharSet=utf8mb4;" "data/canteen.db"
```

## 迁移流程

1. **连接 MySQL 数据库** - 验证连接并读取数据
2. **创建 SQLite 数据库** - 自动创建表结构
3. **数据迁移** - 按依赖顺序迁移各表数据
   - categories (分类表)
   - products (商品表)
   - procurement_tasks (采购任务表)
   - task_category_budgets (任务分类预算表)
   - procurement_details (采购明细表)
   - product_usage_history (商品使用历史表)
   - system_configs (系统配置表)
   - operation_logs (操作日志表)
4. **创建索引和触发器** - 优化查询性能
5. **数据验证** - 对比 MySQL 和 SQLite 记录数

## 数据类型转换

| MySQL 类型 | SQLite 类型 | 转换说明 |
|-----------|------------|---------|
| DATETIME, TIMESTAMP | TEXT | 转换为 ISO8601 格式 |
| DATE | TEXT | 转换为 YYYY-MM-DD 格式 |
| TINYINT(1) | INTEGER | 布尔值转为 0/1 |
| INT, INTEGER | INTEGER | 直接复制 |
| BIGINT | INTEGER | 直接复制 |
| DECIMAL, DOUBLE | REAL | 直接复制 |
| VARCHAR, TEXT | TEXT | 直接复制 |

## 注意事项

1. **备份 MySQL 数据** - 迁移前建议备份原数据库
2. **迁移时间** - 数据量大时可能需要较长时间
3. **字符编码** - 确保 MySQL 使用 utf8mb4 编码
4. **外键约束** - 迁移过程中会启用外键约束
5. **目标文件** - 如目标 SQLite 文件已存在，会被覆盖

## 迁移后步骤

1. 将生成的 SQLite 数据库文件复制到应用程序目录：
   ```
   复制 data/canteen.db 到 src/CanteenProcurement.Wpf/bin/Debug/net10.0-windows/data/
   ```

2. 修改 `appsettings.json`：
   ```json
   {
     "Database": {
       "provider": "Sqlite",
       "sqlite": {
         "databasePath": "data/canteen.db"
       }
     }
   }
   ```

3. 启动应用程序验证数据

## 故障排除

### 连接失败
- 检查 MySQL 服务是否运行
- 验证用户名和密码
- 确认数据库是否存在

### 数据不一致
- 检查 MySQL 字符集是否为 utf8mb4
- 查看错误日志了解具体问题

### 编译错误
- 确保已安装 .NET 10 SDK
- 运行 `dotnet restore` 恢复依赖
