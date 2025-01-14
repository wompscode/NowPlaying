using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System.Runtime.InteropServices;
using NPSMLib;

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

    public static string CurrentSong = "";
    public static string CurrentArtist = "";
    public static bool IsPaused;
    
    static readonly object LockObject = new object();
    private static NowPlayingSessionManager? Manager;
    private static NowPlayingSession? Session;
    private static MediaPlaybackDataSource? Src;
    private readonly ServerInfoDisplay barDisplay;
    public Configuration Configuration { get; init; }
    public static bool ShowInStatusBar;
    public Plugin()
    {
        PluginInterface.Create<Services>();
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ShowInStatusBar = Configuration.ShowInStatusBar;
        
        CommandManager.AddHandler("/nowplaying", new CommandInfo(OnCommand)
        {
            HelpMessage = "args: [current, next, prev, playpause, statusbar]."
        });
        CommandManager.AddHandler("/nowplaying current", new CommandInfo(OnCommand)
        {
            HelpMessage = "Print the current song to chat.."
        });
        CommandManager.AddHandler("/nowplaying next", new CommandInfo(OnCommand)
        {
            HelpMessage = "Skip a song in the current player."
        });
        CommandManager.AddHandler("/nowplaying prev", new CommandInfo(OnCommand)
        {
            HelpMessage = "Go back a song in the current player."
        });
        CommandManager.AddHandler("/nowplaying playpause", new CommandInfo(OnCommand)
        {
            HelpMessage = "Play and pause the currently playing player."
        });
        CommandManager.AddHandler("/nowplaying statusbar", new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle the server info bar element."
        });
        CommandManager.AddHandler("/npl", new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /nowplaying. Supports all the same arguments."
        });
        
        barDisplay = new ServerInfoDisplay();
        Manager = new NowPlayingSessionManager();
        Manager.SessionListChanged += OnSessionListChanged;
        OnSessionListChanged(null,null);
    }

    private readonly bool isSet = false;
    private void OnSessionListChanged(object? sender, NowPlayingSessionManagerEventArgs? e)
    {
        barDisplay.Update();
        if (Manager != null && Session == Manager.CurrentSession) return;
        Log.Debug("OnSessionListChanged hit.");
        
        if (Manager != null)
        {
            Session = Manager.CurrentSession;
            Log.Debug("Session is set.");
            if (Session != null)
            {
                Src = Session.ActivateMediaPlaybackDataSource();
                Log.Debug("Src is set.");
                if (Src != null && isSet == false)
                {
                    Src.MediaPlaybackDataChanged += PlaybackDataChanged;
                    PlaybackDataChanged(null, null);
                    Log.Debug("PlaybackDataChanged triggered.");
                }
                else
                {
                    Log.Debug("Src is null, no session was ever set.");
                }
            }
            else
            {
                Log.Debug("Session is null, no manager.");
            }
        }
        else
        {
            Log.Debug("Manager is null, assume no player.");
            CurrentArtist = "";
            CurrentSong = "";
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
                    Log.Info($"{mediaDetails.Artist} - {mediaDetails.Title}");
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
        CommandManager.RemoveHandler("/npl");
        if(Manager != null) Manager.SessionListChanged -= OnSessionListChanged;
        if(Src != null) Src.MediaPlaybackDataChanged -= PlaybackDataChanged;
        Configuration.Save();
    }

    private void OnCommand(string command, string args)
    {
        Log.Info(args);

        if (command == "/nowplaying" || command == "/npl")
        {
            Log.Info("nowplaying command hit");
            if (args.ToLower().StartsWith("next"))
            {
                Log.Info("Skipping song..");
                keybd_event(0xB0, 0, 1, IntPtr.Zero); // Next song key
            }
            else if (args.ToLower().StartsWith("playpause"))
            {
                Log.Info("Playing/pausing song..");
                keybd_event(0xB3 , 0, 1, IntPtr.Zero); // Play pause key
            } else if (args.ToLower().StartsWith("prev"))
            {
                Log.Info("Going back a song..");
                keybd_event(0xB1 , 0, 1, IntPtr.Zero); // Previous song key
            } else if (args.ToLower().StartsWith("current"))
            {
                Services.ChatGui.Print($"Now playing: {CurrentArtist} - {CurrentSong}");
            } else if (args.ToLower().StartsWith("statusbar"))
            {
                Configuration.ShowInStatusBar = !ShowInStatusBar;
                ShowInStatusBar = Configuration.ShowInStatusBar;
                Log.Debug($"{ShowInStatusBar}, {Configuration.ShowInStatusBar}");
                Configuration.Save();
                Services.ChatGui.Print($"{(ShowInStatusBar ? "Toggled server info bar display on." : "Toggled server info bar display off.")}");
                barDisplay.UpdateDisplay(ShowInStatusBar);
            }
        }
    }
}
