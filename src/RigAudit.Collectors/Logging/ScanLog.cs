using System.Text;

namespace RigAudit.Collectors.Logging;

public class ScanLog
{
    private readonly StringBuilder _sb = new();
    private readonly StringBuilder _debugSb = new();
    private readonly bool _debugEnabled;

    public ScanLog(bool debugEnabled = false)
    {
        _debugEnabled = debugEnabled;
    }

    public void Info(string message)
        => Append("INFO", message);

    public void Ok(string message)
        => Append("OK", message);

    public void Fail(string message)
        => Append("FAIL", message);

    public void Warn(string message)
        => Append("WARN", message);

    public void Debug(string message)
    {
        if (!_debugEnabled)
            return;

        Append(_debugSb, "DEBUG", message);
    }

    public bool HasDebugDetails => _debugEnabled && _debugSb.Length > 0;
    public string DebugToString() => _debugSb.ToString();

    private void Append(string status, string message)
        => Append(_sb, status, message);

    private static void Append(StringBuilder target, string status, string message)
        => target.AppendLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{status}] {message}");

    public override string ToString() => _sb.ToString();
}
