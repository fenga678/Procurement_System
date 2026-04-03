#!/bin/bash
echo "========================================"
echo "  MySQL 到 SQLite 数据迁移工具"
echo "========================================"
echo

cd "$(dirname "$0")"

# 检查是否已编译
if [ ! -f "bin/Debug/net10.0/DataMigrationTool.dll" ]; then
    echo "正在编译迁移工具..."
    dotnet build -c Debug
    if [ $? -ne 0 ]; then
        echo "编译失败！"
        exit 1
    fi
    echo
fi

# 运行迁移工具
dotnet run --project DataMigrationTool.csproj
