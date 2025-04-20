namespace NowPlaying;
/*
 * NowPlaying 1.3.0.0
 *      wompscode
 *
 * i make the things i want and put them up so others who want what i make can have what i make
 */
using System;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using System.Runtime.InteropServices;
using NPSMLib;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

public sealed class Plugin : IDalamudPlugin
{
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
    // Plugin configuration
    public Configuration Configuration { get; init; }

    // Bar element
    private readonly ServerInfoDisplay barDisplay;
    
    // Config options
    public static bool ShowInStatusBar;
    public static bool HideOnPause;

    // Publicly available song data
    public static string CurrentSong = "";
    public static string CurrentArtist = "";
    public static bool IsPaused;

    // Lock stuff
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
        pluginInterface.Create<Services>();
        
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ShowInStatusBar = Configuration.ShowInStatusBar;
        HideOnPause = Configuration.HideOnPause;

        Services.CommandManager.AddHandler("/nowplaying", new CommandInfo(CommandHandler)
        {
            HelpMessage = "args: [current, next, prev, play, pause, playpause, statusbar, hideonpause]."
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
        Services.CommandManager.AddHandler("/nowplaying statusbar", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Toggle the server info bar element."
        });
        Services.CommandManager.AddHandler("/nowplaying hideonpause", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Toggle if the plugin should hide the status bar element on player pause."
        });
        Services.CommandManager.AddHandler("/nowplaying cycle", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Cycle between active players."
        });
        Services.CommandManager.AddHandler("/npl", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Alias for /nowplaying. Supports all the same arguments."
        });
        
        barDisplay = new ServerInfoDisplay(this);
        
        Manager = new NowPlayingSessionManager();
        Manager.SessionListChanged += OnSessionListChanged;
        OnSessionListChanged(null,null);
    }

    public void CycleSession()
    {
        if (Sessions != null)
        {
            Services.PluginLog.Debug($"sessions: {Sessions.Length}");
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
        Services.PluginLog.Debug("OnSessionListChanged hit.");
        if (Manager == null) return;
        barDisplay.Update();
        
        Sessions = Manager.GetSessions();
        
        if (Sessions.Length <= 0)
        {
            // I don't know how I never thought about this. I've always got Spotify running, so I figure at no point did I go "hey, maybe I should check if there are any sessions at all.". Oh well.
            Services.PluginLog.Info("No sessions found, not continuing..");
            return;
        } 
        
        if (SessionIndex >= Sessions.Length) SessionIndex = 0;
        
        Session = Sessions[SessionIndex];
        Services.PluginLog.Debug("Session is set.");
        
        Src = Session.ActivateMediaPlaybackDataSource();
        Services.PluginLog.Debug("Src is set.");
        if (Src != null)
        {
            if (isAttached) return;
            isAttached = true;
            
            Src.MediaPlaybackDataChanged += PlaybackDataChanged;
            PlaybackDataChanged(null, null);
            
            Services.PluginLog.Debug("PlaybackDataChanged triggered.");
        }
        else
        {
            Services.PluginLog.Debug("Src is null, no session was ever set.");
        }
    }

    private void PlaybackDataChanged(object? sender, MediaPlaybackDataChangedArgs? e)
    {
        Services.PluginLog.Debug("PlaybackDataChanged hit.");

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
                    Services.PluginLog.Debug($"{mediaDetails.Artist} - {mediaDetails.Title}");
                    IsPaused = mediaPlaybackInfo.PlaybackState == MediaPlaybackState.Paused;
                    barDisplay.Update();
                }
            }
        }
        else
        {
            Services.PluginLog.Debug("Session is null, so assume player shut.");
            CurrentArtist = "";
            CurrentSong = "";
            barDisplay.Update();
        }
    }

    public void Dispose()
    {
        Services.CommandManager.RemoveHandler("/nowplaying");
        Services.CommandManager.RemoveHandler("/nowplaying current");
        Services.CommandManager.RemoveHandler("/nowplaying next");
        Services.CommandManager.RemoveHandler("/nowplaying prev");
        Services.CommandManager.RemoveHandler("/nowplaying statusbar");
        Services.CommandManager.RemoveHandler("/nowplaying hideonpause");
        Services.CommandManager.RemoveHandler("/nowplaying playpause");
        Services.CommandManager.RemoveHandler("/nowplaying play");
        Services.CommandManager.RemoveHandler("/nowplaying pause");
        Services.CommandManager.RemoveHandler("/nowplaying cycle");
        Services.CommandManager.RemoveHandler("/npl");
        
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
        barDisplay.Dispose();
        Configuration.Save();
    }

    private void CommandHandler(string command, string args)
    {
        Services.PluginLog.Debug(args);

        string[] argsSplit = args.Split(' ');
        
        if (command == "/nowplaying" || command == "/npl")
        {
            Services.PluginLog.Debug("nowplaying command hit");

            if (argsSplit.Length < 1 || string.IsNullOrEmpty(args))
            {
                Services.PluginLog.Debug("No arguments.");
                return;
            }

            string subcommand = argsSplit[0].ToLower();
            Services.PluginLog.Debug($"subcommand: {subcommand}");

            switch (subcommand)
            {
                case "next":
                    Services.PluginLog.Info("Skipping song..");
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
                    Services.PluginLog.Info("Going back a song..");
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
                    Services.PluginLog.Information("Playing song..");
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
                    Services.PluginLog.Information("Pausing song..");
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
                    Services.PluginLog.Info("Playing/pausing song..");
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
                    Services.ChatGui.Print($"Now playing: {CurrentArtist} - {CurrentSong}");
                    break;
                case "statusbar":
                    Configuration.ShowInStatusBar = !ShowInStatusBar;
                    ShowInStatusBar = Configuration.ShowInStatusBar;
                    Services.PluginLog.Debug($"{ShowInStatusBar}, {Configuration.ShowInStatusBar}");
                    Configuration.Save();
                    Services.ChatGui.Print($"{(ShowInStatusBar ? "Toggled server info bar display on." : "Toggled server info bar display off.")}");
                    barDisplay.UpdateDisplay(ShowInStatusBar);
                    break;
                case "hideonpause":
                    Configuration.HideOnPause = !HideOnPause;
                    HideOnPause = Configuration.HideOnPause;
                    Services.PluginLog.Debug($"{HideOnPause}, {Configuration.HideOnPause}");
                    Configuration.Save();
                    Services.ChatGui.Print($"{(HideOnPause ? "Toggled Hide on Pause on." : "Toggled Hide on Pause off.")}");
                    break;
                case "cycle":
                    CycleSession();
                    break;
            }            
        }
    }
}
