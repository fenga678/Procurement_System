@echo off
chcp 65001 >nul
echo ========================================
echo   MySQL 到 SQLite 数据迁移工具
echo ========================================
echo.

cd /d "%~dp0"

:: 检查是否已编译
if not exist "bin\Debug\net10.0\DataMigrationTool.dll" (
    echo 正在编译迁移工具...
    dotnet build -c Debug
    if errorlevel 1 (
        echo 编译失败！
        pause
        exit /b 1
    )
    echo.
)

:: 运行迁移工具
dotnet run --project DataMigrationTool.csproj

pause
