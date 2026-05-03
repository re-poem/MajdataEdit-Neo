namespace MajdataEdit_Neo.Models.SimaiChecker;

public enum Severity
{
    Info,
    Warning, 
    Error
}

/// <summary>
/// 诊断信息
/// </summary>
/// <param name="Severity">严重程度</param>
/// <param name="Message">错误/警告消息</param>
/// <param name="Detail">详细说明</param>
/// <param name="PositionStart">错误位置起点</param>
/// <param name="length">错误长度</param>
public record class SimaiDiagnostic(
    Severity Severity,
    string Message, string Detail,
    TextPosition PositionStart, int length);

