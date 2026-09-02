using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Game.Config;
using Dalamud.Plugin;
using Dalamud.Utility;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.ImGuiNotification;
using NowPlaying.MediaControllers;
using Notification = Dalamud.Interface.ImGuiNotification.Notification;

namespace NowPlaying;

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
    public static bool MuteBgmOnPlay;
    public static bool Truncate;
    public static int  MaxSongChars;
    public static int  MaxArtistChars;

    // Song data
    public static string CurrentSong => MediaController?.CurrentSong ?? string.Empty;
    public static string CurrentArtist => MediaController?.CurrentArtist ?? string.Empty;
    public static string CurrentAlbum => MediaController?.CurrentAlbum ?? string.Empty;
    public static bool IsPaused => MediaController?.IsPaused ?? true;
    
    // IsWine result
    private static bool IsWine;
    private static IMediaController? MediaController;
    
    // mute handling
    private bool mutedGame;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        IsWine = Util.IsWine();
        
        pluginInterface.Create<Services>();
        Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ShowInStatusBar = Configuration.ShowInStatusBar;
        HideOnPause = Configuration.HideOnPause;
        MuteBgmOnPlay = Configuration.MuteBgmOnPlay;
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
        Services.CommandManager.AddHandler("/nowplaying mutebgmonplay", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Whether to mute the game's BGM while music is playing.",
        });
        Services.CommandManager.AddHandler("/npl", new CommandInfo(CommandHandler)
        {
            HelpMessage = "Alias for /nowplaying. Supports all the same arguments."
        });
        
        dtrDisplay = new ServerInfoDisplay(this);

        if (IsWine)
            MediaController = new MprisMediaController();
        else
            MediaController = new NpsmMediaController();

        MediaController.OnUpdated += OnMediaControllerUpdated;

        try
        {
            MediaController.Start();
        }
        catch (Tmds.DBus.Protocol.DBusConnectionException e)
        {
            var notification = new Notification
            {
                Type = NotificationType.Error,
                Title = "Failed to connect to media players",
                Content = e.InnerException is SocketException { SocketErrorCode: SocketError.AddressFamilyNotSupported }
                              ? "Cannot connect to media players because Unix sockets are not supported on this version of Wine/Proton. Please try another version of Wine/Proton."
                              : "An unknown error occured, check logs for details.",
                Minimized = false,
                InitialDuration = TimeSpan.MaxValue,
            };

            Services.NotificationManager.AddNotification(notification);
            throw;
        }
        catch (Exception)
        {
            var notification = new Notification
            {
                Type = NotificationType.Error,
                Title = "Failed to connect to media players",
                Content = "An unknown error occured, check logs for details.",
                Minimized = false,
                InitialDuration = TimeSpan.MaxValue,
            };

            Services.NotificationManager.AddNotification(notification);
            throw;
        }
    }

    private void OnMediaControllerUpdated(object? sender, EventArgs args)
    {
        if (!IsPaused && MuteBgmOnPlay && Services.GameConfig.TryGet(SystemConfigOption.IsSndBgm, out bool muted) && !muted)
        {
            Services.GameConfig.Set(SystemConfigOption.IsSndBgm, true);
            mutedGame = true;
        }

        if (IsPaused && MuteBgmOnPlay && mutedGame)
        {
            Services.GameConfig.Set(SystemConfigOption.IsSndBgm, false);
            mutedGame = false;
        }
        
        dtrDisplay.Update();
    }

    public void CycleSessionDtr(DtrInteractionEvent interactionEvent)
    {
        CycleSession();
    }
    
    public static void CycleSession()
    {
        MediaController?.CycleSession();
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
        Services.CommandManager.RemoveHandler("/nowplaying mutebgmonplay");
        Services.CommandManager.RemoveHandler("/npl");
        
        if (MuteBgmOnPlay && mutedGame)
            Services.GameConfig.Set(SystemConfigOption.IsSndBgm, false);
        
        MediaController?.Dispose();
        dtrDisplay.Dispose();
        Configuration.Save();
    }

    private void CommandHandler(string command, string args)
    {
        string[] argsSplit = args.Split(' ');
        
        if (command == "/nowplaying" || command == "/npl")
        {
            if (argsSplit.Length < 1 || string.IsNullOrEmpty(args))
            {
                    Services.ChatGui.Print("[NowPlaying] valid subcommands: current, next, prev, play, pause, playpause, sb, hop, msc <int>, mac <int>, trunc.");
                return;
            }

            string subcommand = argsSplit[0].ToLower();

            switch (subcommand)
            {
                case "next":
                    if ((MediaController == null || !MediaController.TryNext()) && !IsPaused)
                        keybd_event(0xB0, 0, 1, IntPtr.Zero); // Next song key
                    break;
                case "prev":
                    if ((MediaController == null || !MediaController.TryPrevious()) && !IsPaused)
                        keybd_event(0xB1 , 0, 1, IntPtr.Zero); // Previous song key
                    break;
                case "play":
                    if ((MediaController == null || !MediaController.TryPlay()) && IsPaused)
                        keybd_event(0xB3 , 0, 1, IntPtr.Zero); // Play pause key
                    break;
                case "pause":
                    if ((MediaController == null || !MediaController.TryPause()) && !IsPaused)
                        keybd_event(0xB3 , 0, 1, IntPtr.Zero); // Play pause key
                    break;
                case "playpause":
                    if (MediaController == null || !MediaController.TryPlayPauseToggle())
                        keybd_event(0xB3 , 0, 1, IntPtr.Zero); // Play pause key
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
                case "mutebgmonplay":
                    MuteBgmOnPlay = Configuration.MuteBgmOnPlay ^= true;
                    Configuration.Save();
                    
                    if (MuteBgmOnPlay)
                        Services.ChatGui.Print("[NowPlaying] Enabled muting game BGM when playing media.");
                    else
                        Services.ChatGui.Print("[NowPlaying] Disabled muting game BGM when playing media.");
                    break;
                    
            }            
        }
    }
}
