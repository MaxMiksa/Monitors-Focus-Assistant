namespace MonitorsFocus.Brightness;

internal interface IBrightnessDevice : IDisposable
{
    int ReadBrightness();
    void SetBrightness(int percent);
}
