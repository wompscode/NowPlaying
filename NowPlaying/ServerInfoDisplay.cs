﻿using Dalamud.Game.Gui.Dtr;
// ReSharper disable StringLiteralTypo
namespace NowPlaying;

public class ServerInfoDisplay
{
    private readonly IDtrBarEntry entry;
    
    public ServerInfoDisplay(Plugin instance)
    {
        entry = Services.DtrBar.Get("NowPlaying");
        entry.Shown = Plugin.ShowInStatusBar;
        entry.OnClick = instance.CycleSession;
    }

    public void Dispose()
    {
        entry.Remove();
    }

    public void UpdateDisplay(bool state)
    {
        Services.PluginLog.Debug(state ? "Enabled" : "Disabled");
        if (Plugin.IsPaused && Plugin.HideOnPause)
        {
            entry.Shown = false;
            return;
        }
        entry.Shown = state;
    }
    public void Update()
    {
        var song = Plugin.CurrentSong;
        var artist = Plugin.CurrentArtist;

        if (string.IsNullOrEmpty(song) && string.IsNullOrEmpty(artist))
        {
            entry.Shown = false;
            return;
        }

        if (string.IsNullOrEmpty(song)) song = "n/a";
        if (string.IsNullOrEmpty(artist)) artist = "n/a";

        Services.PluginLog.Debug($"HOP: {Plugin.HideOnPause}");
        
        if (Plugin.IsPaused && Plugin.HideOnPause)
        {
            Services.PluginLog.Debug("Hiding server bar info..");
            entry.Shown = false;
            return;
        }
        
        Services.PluginLog.Debug($"SISB: {Plugin.ShowInStatusBar}");

        entry.Shown = Plugin.ShowInStatusBar;
        var indicator = Plugin.IsPaused ? "||" : ">";
        
        var displayNonTruncated = $"{artist} - {song}";
        if (artist.Length > 18) artist = artist.Substring(0, 18) + "..";
        if (song.Length > 24) song = song.Substring(0, 24) + "..";
        var display = $"{artist} - {song}";

        if (display.Length >= 50)
        {
            entry.Text = indicator + " " + display.Substring(0, 50) + "...";
            entry.Tooltip = displayNonTruncated;
        }
        else
        {
            entry.Text = indicator + " " + display;
            entry.Tooltip = displayNonTruncated;
        }
    }
}
