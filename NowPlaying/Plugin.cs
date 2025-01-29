using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Runtime.InteropServices;
using NPSMLib;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

// This is not the most clean, but this was started initially as just me figuring out if I could do the things I wanted.

namespace NowPlaying;
public sealed class Plugin : IDalamudPlugin
{
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, IntPtr extraInfo);
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private readonly ServerInfoDisplay barDisplay;
    public static bool ShowInStatusBar;
    public static bool HideOnPause;

    public static string CurrentSong = "";
    public static string CurrentArtist = "";
    public static bool IsPaused;

    private bool isAttached = false;
    
    static readonly object LockObject = new object();
    
    private static NowPlayingSessionManager? Manager;
    private static NowPlayingSession? Session;
    private static MediaPlaybackDataSource? Src;
    
    public static NowPlayingSession[]? Sessions;
    public static int SessionIndex;
    public Configuration Configuration { get; init; }

    public Plugin()
    {
        PluginInterface.Create<Services>();
        
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ShowInStatusBar = Configuration.ShowInStatusBar;
        HideOnPause = Configuration.HideOnPause;

        CommandManager.AddHandler("/nowplaying", new CommandInfo(CommandHandler)
        {
            HelpMessage = "args: [current, next, prev, play, pause, playpause, statusbar, hideonpause]."
        });
        CommandManager.AddHandler("/nowplaying current", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Print the current song to chat."
        });
        CommandManager.AddHandler("/nowplaying next", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Skip a song in the current player."
        });
        CommandManager.AddHandler("/nowplaying prev", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Go back a song in the current player."
        });
        CommandManager.AddHandler("/nowplaying playpause", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Play and pause the currently playing player."
        });
        CommandManager.AddHandler("/nowplaying play", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Resume the currently playing player."
        });
        CommandManager.AddHandler("/nowplaying pause", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Pause the currently playing player."
        });
        CommandManager.AddHandler("/nowplaying statusbar", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Toggle the server info bar element."
        });
        CommandManager.AddHandler("/nowplaying hideonpause", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Toggle if the plugin should hide the status bar element on player pause."
        });
        CommandManager.AddHandler("/nowplaying cycle", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Cycle between active players."
        });
        CommandManager.AddHandler("/npl", new CommandInfo(CommandHandler)
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
            Log.Debug($"sessions: {Sessions.Length}");
            SessionIndex += 1;
            if (SessionIndex >= Sessions.Length) SessionIndex = 0;
        }

        if (Src != null)
        {
            Src.MediaPlaybackDataChanged -= PlaybackDataChanged;
            isAttached = false;
        }
        OnSessionListChanged(null, null);
    }
    
    private void OnSessionListChanged(object? sender, NowPlayingSessionManagerEventArgs? e)
    {
        Log.Debug("OnSessionListChanged hit.");
        if (Manager == null) return;
        barDisplay.Update();
        
        Sessions = Manager.GetSessions();
        
        if (SessionIndex >= Sessions.Length) SessionIndex = 0;
        Session = Sessions[SessionIndex];
        
        Log.Debug("Session is set.");
        
        Src = Session.ActivateMediaPlaybackDataSource();
        Log.Debug("Src is set.");
        if (Src != null)
        {
            if (isAttached) return;
            isAttached = true;
            
            Src.MediaPlaybackDataChanged += PlaybackDataChanged;
            PlaybackDataChanged(null, null);
            
            Log.Debug("PlaybackDataChanged triggered.");
        }
        else
        {
            Log.Debug("Src is null, no session was ever set.");
        }
    }

    private void PlaybackDataChanged(object? sender, MediaPlaybackDataChangedArgs? e)
    {
        Log.Debug("PlaybackDataChanged hit.");

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
                    Log.Debug($"{mediaDetails.Artist} - {mediaDetails.Title}");
                    IsPaused = mediaPlaybackInfo.PlaybackState == MediaPlaybackState.Paused;
                    barDisplay.Update();
                }
            }
        }
        else
        {
            Log.Debug("Session is null, so assume player shut.");
            CurrentArtist = "";
            CurrentSong = "";
            barDisplay.Update();
        }
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler("/nowplaying");
        CommandManager.RemoveHandler("/nowplaying current");
        CommandManager.RemoveHandler("/nowplaying next");
        CommandManager.RemoveHandler("/nowplaying prev");
        CommandManager.RemoveHandler("/nowplaying statusbar");
        CommandManager.RemoveHandler("/nowplaying hideonpause");
        CommandManager.RemoveHandler("/nowplaying playpause");
        CommandManager.RemoveHandler("/nowplaying play");
        CommandManager.RemoveHandler("/nowplaying pause");
        CommandManager.RemoveHandler("/nowplaying cycle");
        CommandManager.RemoveHandler("/npl");
        
        if(Manager != null) Manager.SessionListChanged -= OnSessionListChanged;
        if(Src != null) Src.MediaPlaybackDataChanged -= PlaybackDataChanged;
        
        Configuration.Save();
    }

    private void CommandHandler(string command, string args)
    {
        Log.Debug(args);

        string[] argsSplit = args.Split(' ');
        
        if (command == "/nowplaying" || command == "/npl")
        {
            Log.Debug("nowplaying command hit");

            if (argsSplit.Length < 1 || string.IsNullOrEmpty(args))
            {
                Log.Debug("No arguments.");
                return;
            }

            string subcommand = argsSplit[0].ToLower();
            Log.Debug($"subcommand: {subcommand}");

            switch (subcommand)
            {
                case "next":
                    Log.Info("Skipping song..");
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
                    Log.Info("Going back a song..");
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
                    Log.Information("Playing song..");
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
                    Log.Information("Pausing song..");
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
                    Log.Info("Playing/pausing song..");
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
                    Log.Debug($"{ShowInStatusBar}, {Configuration.ShowInStatusBar}");
                    Configuration.Save();
                    Services.ChatGui.Print($"{(ShowInStatusBar ? "Toggled server info bar display on." : "Toggled server info bar display off.")}");
                    barDisplay.UpdateDisplay(ShowInStatusBar);
                    break;
                case "hideonpause":
                    Configuration.HideOnPause = !HideOnPause;
                    HideOnPause = Configuration.HideOnPause;
                    Log.Debug($"{HideOnPause}, {Configuration.HideOnPause}");
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
