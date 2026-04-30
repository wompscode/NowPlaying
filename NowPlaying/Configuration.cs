using Dalamud.Configuration;
using System;

namespace NowPlaying;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool ShowInStatusBar { get; set; } = true;
    public bool HideOnPause { get; set; } = false;
    public bool Truncate { get; set; } = true;
    public int MaxSongChars { get; set; } = 24;
    public int MaxArtistChars { get; set; } = 18;

    public void Save()
    {
        Services.PluginInterface.SavePluginConfig(this);
    }
}
