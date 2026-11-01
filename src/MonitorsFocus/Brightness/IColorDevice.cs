namespace MonitorsFocus.Brightness;

internal interface IColorDevice : IBrightnessDevice
{
    uint[]? ReadGains();
    void SetGains(uint[] values);
    void RestoreGains(uint[] values);
}
