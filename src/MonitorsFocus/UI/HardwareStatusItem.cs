namespace MonitorsFocus.UI;

internal sealed class HardwareStatusItem
{
    public HardwareStatusItem(string id, string status, string? message, bool enabled)
    {
        Id = id;
        Status = status;
        Message = message;
        Enabled = enabled;
    }

    public string Id { get; }
    public string Status { get; }
    public string? Message { get; }
    public bool Enabled { get; }
}
