using System;
using System.Data;
using System.IO;
using System.Windows;
using Microsoft.Data.Sqlite;

namespace CanteenProcurement.Wpf.Services;

/// <summary>
/// 统一异常分类与处理
/// </summary>
public static class ExceptionHandler
{
    /// <summary>
    /// 将异常分类为用户可理解的提示
    /// </summary>
    public static (string Title, string Message) Classify(Exception ex)
    {
        return ex switch
        {
            InvalidOperationException ie => ("操作无效", ie.Message),
            ArgumentException ae => ("输入错误", ae.Message),
            SqliteException se => ("数据库错误", GetDatabaseException(se)),
            IOException io => ("文件读写错误", io.Message),
            UnauthorizedAccessException ua => ("权限错误", "程序没有足够的权限执行此操作。"),
            _ => ("系统错误", $"发生未知错误：{ex.Message}")
        };
    }

    private static string GetDatabaseException(SqliteException ex)
    {
        var code = ex.SqliteErrorCode;
        return code switch
        {
            19 => "数据违反唯一性约束，可能已存在重复记录。",
            5 => "数据库文件被锁定，请稍后重试。",
            14 => "无法打开数据库文件，请检查文件路径和权限。",
            _ => $"数据库操作失败（错误码 {code}）：{ex.Message}"
        };
    }

    /// <summary>
    /// 在页面中显示异常
    /// </summary>
    public static void ShowInWindow(Exception ex, Window? owner = null, string context = "")
    {
        var (title, message) = Classify(ex);
        var displayMessage = string.IsNullOrWhiteSpace(context)
            ? message
            : $"{context}：{message}";
        
        AppDialogService.ShowError(owner, title, displayMessage);
    }
}
