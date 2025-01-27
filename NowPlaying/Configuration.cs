using Dalamud.Configuration;
using System;

namespace NowPlaying;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool ShowInStatusBar { get; set; } = true;
    public bool HideOnPause { get; set; } = true;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
