using Dalamud.Game.Gui.Dtr;
// ReSharper disable StringLiteralTypo
namespace NowPlaying;

public class ServerInfoDisplay
{
    private readonly IDtrBarEntry entry;
    
    public ServerInfoDisplay(Plugin instance)
    {
        entry = Services.DtrBar.Get("NowPlaying");
        entry.Shown = Plugin.ShowInStatusBar;
        entry.OnClick = instance.CycleSessionDtr;
    }

    public void Dispose()
    {
        entry.Remove();
    }

    public void UpdateDisplay(bool state)
    {
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
        var album = Plugin.CurrentAlbum;

        if (string.IsNullOrEmpty(song) && string.IsNullOrEmpty(artist))
        {
            entry.Shown = false;
            return;
        }

        if (string.IsNullOrEmpty(song)) song = "n/a";
        if (string.IsNullOrEmpty(artist)) artist = "n/a";
        if (string.IsNullOrEmpty(album)) album = "n/a";

        if (Plugin.IsPaused && Plugin.HideOnPause)
        {
            entry.Shown = false;
            return;
        }
        
        entry.Shown = Plugin.ShowInStatusBar;
        var indicator = Plugin.IsPaused ? "||" : ">";
        
        var tooltip = $"{song} by {artist}{(album == "n/a" ? "." : $" on {album}.")}";

        if (artist.Length > Plugin.MaxArtistChars && Plugin.Truncate) artist = artist.Substring(0, Plugin.MaxArtistChars) + "..";
        if (song.Length > Plugin.MaxSongChars && Plugin.Truncate) song = song.Substring(0, Plugin.MaxSongChars) + "..";
        int maxFullLength = Plugin.MaxSongChars + Plugin.MaxArtistChars + 4;
        var display = $"♪ {indicator} {song} by {artist}";

        entry.Tooltip = tooltip;
        if (Plugin.Truncate)
        {
            entry.Text = display.Length >= maxFullLength ? $"{display.Substring(0, maxFullLength)}.." : $"{display}";
        }
        else
        {
            entry.Text = $"{display}";
        }
    }
}
