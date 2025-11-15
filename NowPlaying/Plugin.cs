namespace NowPlaying;

using System;
using System.Runtime.InteropServices;

using NPSMLib;

using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Game.Gui.Dtr;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

public sealed class Plugin : IDalamudPlugin
{
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
 
    // Plugin configuration
    public Configuration Configuration { get; init; }

    // Bar element
    private readonly ServerInfoDisplay dtrDisplay;
    
    // Config options
    public static bool ShowInStatusBar;
    public static bool HideOnPause;
    public static bool Truncate;
    public static int  MaxSongChars;
    public static int  MaxArtistChars;

    // Song data
    public static string CurrentSong = "";
    public static string CurrentArtist = "";
    public static string CurrentAlbum = "";
    public static bool   IsPaused;
    
    // IsWine result
    private static bool IsWine;
    
    // Single-thread lock + check bool
    private bool isAttached;
    static readonly object LockObject = new ();

    // SMTC
    private static NowPlayingSessionManager? Manager;
    private static NowPlayingSession? Session;
    private static MediaPlaybackDataSource? Src;
    public static NowPlayingSession[]? Sessions;
    public static int SessionIndex;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        IsWine = Util.IsWine();
        
        pluginInterface.Create<Services>();
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ShowInStatusBar = Configuration.ShowInStatusBar;
        HideOnPause = Configuration.HideOnPause;
        Truncate = Configuration.Truncate;
        MaxSongChars = Configuration.MaxSongChars;
        MaxArtistChars = Configuration.MaxArtistChars;
        
        Services.CommandManager.AddHandler("/nowplaying", new CommandInfo(CommandHandler)
        {
            HelpMessage = "args: [current, next, prev, play, pause, playpause, statusbar, hideonpause, maxsongchars <int>, maxartistchars <int>, trunc]."
        });
        Services.CommandManager.AddHandler("/nowplaying current", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Print the current song to chat."
        });
        Services.CommandManager.AddHandler("/nowplaying next", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Skip a song in the current player."
        });
        Services.CommandManager.AddHandler("/nowplaying prev", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Go back a song in the current player."
        });
        Services.CommandManager.AddHandler("/nowplaying playpause", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Play and pause the currently playing player."
        });
        Services.CommandManager.AddHandler("/nowplaying play", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Resume the currently playing player."
        });
        Services.CommandManager.AddHandler("/nowplaying pause", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Pause the currently playing player."
        });
        Services.CommandManager.AddHandler("/nowplaying sb", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Toggle the server info bar element."
        });
        Services.CommandManager.AddHandler("/nowplaying hop", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Toggle if the plugin should hide the status bar element on player pause."
        });
        Services.CommandManager.AddHandler("/nowplaying trunc", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Toggle if the plugin should truncate the status bar text if it is too long."
        });
        Services.CommandManager.AddHandler("/nowplaying msc", new CommandInfo(CommandHandler)
        {
            HelpMessage = "How many characters should be visible before truncating the song title?"
        });
        Services.CommandManager.AddHandler("/nowplaying mac", new CommandInfo(CommandHandler)
        {
            HelpMessage = "How many characters should be visible before truncating the artist's name?"
        });
        Services.CommandManager.AddHandler("/nowplaying cycle", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Cycle between active players."
        });
        Services.CommandManager.AddHandler("/npl", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Alias for /nowplaying. Supports all the same arguments."
        });
        
        dtrDisplay = new ServerInfoDisplay(this);
        if (!IsWine)
        {
            Manager = new NowPlayingSessionManager();
            Manager.SessionListChanged += OnSessionListChanged;
            OnSessionListChanged(null,null);
        }
    }

    public void CycleSessionDtr(DtrInteractionEvent interactionEvent)
    {
        CycleSession();
    }
    
    public void CycleSession()
    {
        if (IsWine) return;

        if (Sessions != null)
        {
            SessionIndex += 1;
            if (SessionIndex >= Sessions.Length) SessionIndex = 0;
        }

        if (Src != null)
        {
            try
            {
                Src.MediaPlaybackDataChanged -= PlaybackDataChanged;
            }
            catch  (Exception e)
            {
                // might not be the same source as it was before so if we try to unhook, it'll get upset but it largely can be ignored. i dont care. it works.
                Services.PluginLog.Warning("Issue with unhooking Src.MediaPlaybackDataChanged, this error can likely be ignored as the playback source just likely was closed (error: {0}).", e.Message);
            }
            isAttached = false;
        }
        OnSessionListChanged(null, null);
    }
    private void OnSessionListChanged(object? sender, NowPlayingSessionManagerEventArgs? e)
    {
        if (IsWine) return;

        if (Manager == null) return;
        dtrDisplay.Update();
        
        Sessions = Manager.GetSessions();
        
        // I don't know how I never thought about this. I've always got Spotify running, so I figure at no point did I go "hey, maybe I should check if there are any sessions at all.". Oh well.
        if (Sessions.Length <= 0)
            return;
        
        if (SessionIndex >= Sessions.Length) SessionIndex = 0;
        
        Session = Sessions[SessionIndex];
        Services.PluginLog.Debug("Session is set.");
        
        Src = Session.ActivateMediaPlaybackDataSource();
        Services.PluginLog.Debug("Src is set.");
        
        if (Src != null)
        {
            if (isAttached) return;
            
            Src.MediaPlaybackDataChanged += PlaybackDataChanged;
            PlaybackDataChanged(null, null);
            isAttached = true;
            
            Services.PluginLog.Verbose("PlaybackDataChanged triggered.");
        }
        else
        {
            Services.PluginLog.Verbose("Src is null, no session was ever set.");
        }
    }

    private void PlaybackDataChanged(object? sender, MediaPlaybackDataChangedArgs? e)
    {
        if (IsWine) return;
        
        if (Session != null)
        {
            lock (LockObject)
            {
                if (Src != null)
                {
                    var mediaDetails = Src.GetMediaObjectInfo();
                    var mediaPlaybackInfo = Src.GetMediaPlaybackInfo();
                    
                    CurrentArtist = mediaDetails.Artist;
                    CurrentSong = mediaDetails.Title;
                    CurrentAlbum = mediaDetails.AlbumTitle;
                    IsPaused = mediaPlaybackInfo.PlaybackState == MediaPlaybackState.Paused;
                    
                    dtrDisplay.Update();
                }
            }
        }
        else
        {
            Services.PluginLog.Verbose("Session is null, so assume player shut.");
            CurrentArtist = "";
            CurrentSong = "";
            dtrDisplay.Update();
        }
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler("/nowplaying");
        Services.CommandManager.RemoveHandler("/nowplaying current");
        Services.CommandManager.RemoveHandler("/nowplaying next");
        Services.CommandManager.RemoveHandler("/nowplaying prev");
        Services.CommandManager.RemoveHandler("/nowplaying sb");
        Services.CommandManager.RemoveHandler("/nowplaying hop");
        Services.CommandManager.RemoveHandler("/nowplaying msc");
        Services.CommandManager.RemoveHandler("/nowplaying mac");
        Services.CommandManager.RemoveHandler("/nowplaying trunc");
        Services.CommandManager.RemoveHandler("/nowplaying playpause");
        Services.CommandManager.RemoveHandler("/nowplaying play");
        Services.CommandManager.RemoveHandler("/nowplaying pause");
        Services.CommandManager.RemoveHandler("/nowplaying cycle");
        Services.CommandManager.RemoveHandler("/npl");

        if (!IsWine)
        {
            if(Manager != null) Manager.SessionListChanged -= OnSessionListChanged;
            try
            {
                if (Src != null && isAttached) Src.MediaPlaybackDataChanged -= PlaybackDataChanged;
            }
            catch  (Exception e)
            {
                // might not be the same source as it was before so if we try to unhook, it'll get upset but it largely can be ignored. i dont care. it works.
                Services.PluginLog.Warning("Issue with unhooking Src.MediaPlaybackDataChanged, this error can likely be ignored as the playback source just likely was closed (error: {0}).", e.Message);
            }
        }
        dtrDisplay.Dispose();
        Configuration.Save();
    }

    private void CommandHandler(string command, string args)
    {
        string[] argsSplit = args.Split(' ');
        
        if (command == "/nowplaying" || command == "/npl")
        {
            if (IsWine)
            {
                    Services.ChatGui.Print("NowPlaying only works under Windows. This plugin will do nothing as it has detected you are running from within a WINE environment. Sorry!");
                return;
            }

            if (argsSplit.Length < 1 || string.IsNullOrEmpty(args))
            {
                    Services.ChatGui.Print("[NowPlaying] valid subcommands: current, next, prev, play, pause, playpause, sb, hop, msc <int>, mac <int>, trunc.");
                return;
            }

            string subcommand = argsSplit[0].ToLower();

            switch (subcommand)
            {
                case "next":
                    if (Src != null)
                    {
                        Src.SendMediaPlaybackCommand(MediaPlaybackCommands.Next);
                    }
                    else
                    {
                        if (!IsPaused)
                        {
                            keybd_event(0xB0, 0, 1, IntPtr.Zero); // Next song key
                        }
                    }
                    break;
                case "prev":
                    if (Src != null)
                    {
                        Src.SendMediaPlaybackCommand(MediaPlaybackCommands.Previous);
                    }
                    else
                    {
                        if (!IsPaused)
                        {
                            keybd_event(0xB1 , 0, 1, IntPtr.Zero); // Previous song key
                        }
                    }
                    break;
                case "play":
                    if (Src != null)
                    {
                        Src.SendMediaPlaybackCommand(MediaPlaybackCommands.Play);
                    }
                    else
                    {
                        if (!IsPaused)
                        {
                            keybd_event(0xB3 , 0, 1, IntPtr.Zero); // Play pause key
                        }
                    }
                    break;
                case "pause":
                    if (Src != null)
                    {
                        Src.SendMediaPlaybackCommand(MediaPlaybackCommands.Pause);
                    }
                    else
                    {
                        if (!IsPaused)
                        {
                            keybd_event(0xB3 , 0, 1, IntPtr.Zero); // Play pause key
                        }
                    }
                    break;
                case "playpause":
                    if (Src != null)
                    {
                        Src.SendMediaPlaybackCommand(MediaPlaybackCommands.PlayPauseToggle);
                    }
                    else
                    {
                        keybd_event(0xB3 , 0, 1, IntPtr.Zero); // Play pause key
                    }
                    break;
                case "current":
                        Services.ChatGui.Print($"[NowPlaying] {CurrentArtist} - {CurrentSong}");
                    break;
                case "sb":
                        Configuration.ShowInStatusBar = !ShowInStatusBar;
                        ShowInStatusBar = Configuration.ShowInStatusBar;
                        Configuration.Save();
                        dtrDisplay.UpdateDisplay(ShowInStatusBar);
                    break;
                case "hop":
                        Configuration.HideOnPause = !HideOnPause;
                        HideOnPause = Configuration.HideOnPause;
                        Configuration.Save();
                        Services.ChatGui.Print($"[NowPlaying] {(HideOnPause ? "Toggled Hide on Pause on." : "Toggled Hide on Pause off.")}");
                    break;
                case "trunc":
                        Configuration.Truncate = !Truncate;
                        Truncate = Configuration.Truncate;
                        Configuration.Save();
                        Services.ChatGui.Print($"[NowPlaying] {(Truncate ? "Toggled truncation on." : "Toggled truncation off.")}");
                    break;
                case "msc":
                    if (argsSplit.Length <= 1)
                    {
                        Services.ChatGui.Print("[NowPlaying] MaxSongChars requires at least one argument.");
                        return;
                    }
                    if (!string.IsNullOrEmpty(argsSplit[1]))
                    {
                        if (int.TryParse(argsSplit[1], out int res))
                        {
                            Configuration.MaxSongChars = res;
                            MaxSongChars = Configuration.MaxSongChars;
                            Configuration.Save();
                            dtrDisplay.Update();
                            Services.ChatGui.Print($"[NowPlaying] MaxSongChars set to {Configuration.MaxSongChars}.");
                        }
                        else
                        {
                            Services.ChatGui.Print($"[NowPlaying] {argsSplit[1]} is not a valid integer");
                        }
                    }
                    break;
                case "mac":
                    if (argsSplit.Length <= 1)
                    {
                        Services.ChatGui.Print("[NowPlaying] MaxArtistChars requires at least one argument.");
                        return;
                    }
                    if (!string.IsNullOrEmpty(argsSplit[1]))
                    {
                        if (int.TryParse(argsSplit[1], out int res))
                        {
                            Configuration.MaxArtistChars = res;
                            MaxArtistChars = Configuration.MaxArtistChars;
                            Configuration.Save();
                            dtrDisplay.Update();
                            Services.ChatGui.Print($"[NowPlaying] MaxArtistChars set to {Configuration.MaxArtistChars}.");
                        }
                        else
                        {
                            Services.ChatGui.Print($"[NowPlaying] {argsSplit[1]} is not a valid integer");
                        }
                    }
                    break;
                case "cycle":
                        CycleSession();
                    break;
            }            
        }
    }
}
