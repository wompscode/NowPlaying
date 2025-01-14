using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace NowPlaying;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool ShowInStatusBar { get; set; }= true;
    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
