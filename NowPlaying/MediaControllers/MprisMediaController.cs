using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mpris.DBus;
using NowPlaying.MediaControllers.DBus;
using Tmds.DBus.Protocol;

namespace NowPlaying.MediaControllers;

public class MprisMediaController : IMediaController
{
    private readonly DBusConnection dbus;
    private IDisposable? dbusMatchRule;
    private List<string> playerNames = [];
    private int playerNameIndex;
    private DBusService? mpris;
    private Player? player;
    private IDisposable? playerPropertyChangeObserver;

    private readonly SemaphoreSlim lockObject = new(1, 1);
    
    public MprisMediaController()
    {
        Services.PluginLog.Debug("Connecting to the D-Bus session bus: {SessionBusAddress}", DBusAddress.Session!);
        dbus = new DBusConnection(new WineDBusConnectionOptions(DBusAddress.Session!));
    }

    public void Start()
    {
        InitAsync().AsTask().Wait();
    }

    public async ValueTask InitAsync(CancellationToken token = default)
    {
        // Connect to the session bus
        await dbus.ConnectAsync();
        Services.PluginLog.Debug("Connected to the D-Bus session bus.");

        // Add an event listener to listen for new players
        var rule = new MatchRule
        {
            Type = MessageType.Signal,
            Sender = "org.freedesktop.DBus",
            Path = "/org/freedesktop/DBus",
            Interface = "org.freedesktop.DBus",
            Member = "NameOwnerChanged",
            // Arg0Namespace doesn't work due to the library spelling it arg0Namespace and not arg0namespace
        };

        dbusMatchRule = await dbus.AddMatchAsync(
                            rule,
                            ReadNameOwnerChangedMessage,
                            HandleNameOwnerChangedSignal,
                            emitOnCapturedContext: false);
        
        // That covers adding/removing future services. However, we still need to fetch the list of services once.
        var players = (await dbus.ListServicesAsync()).Where(s => s.StartsWith("org.mpris.MediaPlayer2."));

        await lockObject.WaitAsync(token);
        try
        {
            foreach (var name in players)
            {
                if (playerNames.Contains(name))
                    continue;

                Services.PluginLog.Debug("MediaPlayer2 service discovered: {Name}", name);
                playerNames.Add(name);
            }
            
            if (playerNames.Count <= 0)
                return;

            if (playerNameIndex >= playerNames.Count)
                playerNameIndex = 0;

            await UpdatePlayer();
        } 
        finally
        {
            lockObject.Release();
        }
    }
    
    public void Dispose()
    {
        playerPropertyChangeObserver?.Dispose();
        dbusMatchRule?.Dispose();
        dbus.Dispose();
        GC.SuppressFinalize(this);
    }

    private static (string, string, string)? ReadNameOwnerChangedMessage(Message message, object? state)
    {
        if (message.Signature.Length != 3
            || message.Signature[0] != 's'
            || message.Signature[1] != 's'
            || message.Signature[2] != 's')
            return null;
                
        var reader = message.GetBodyReader();
        var name = reader.ReadString();

        if (!name.StartsWith("org.mpris.MediaPlayer2."))
            return null;
                
        var oldOwner = reader.ReadString();
        var newOwner = reader.ReadString();

        return (name, oldOwner, newOwner);
    }

    private async ValueTask HandleNameOwnerChangedSignal(Notification<(string, string, string)?> notification)
    {
        if (!notification.HasValue || notification.Value is not var (name, oldOwner, newOwner))
            return;

        // ReSharper disable once MethodSupportsCancellation
        await lockObject.WaitAsync();
        try
        {
            if (oldOwner == string.Empty && newOwner != string.Empty && !playerNames.Contains(name))
            {
                Services.PluginLog.Debug("New MediaPlayer2 service added: {Name}", name);
                playerNames.Add(name);
            }
            else if (oldOwner != string.Empty && newOwner == string.Empty)
            {
                Services.PluginLog.Debug("MediaPlayer2 service removed: {Name}", name);
                playerNames.Remove(name);
            }
            else
            {
                Services.PluginLog.Debug("MediaPlayer2 NameOwnerChanged: Name={Name} OldOwner={OldOwner} NewOwner={NewOwner}", name, oldOwner, newOwner);
                return;
            }
                    
            if (playerNames.Count <= 0)
                return;

            if (playerNameIndex >= playerNames.Count)
                playerNameIndex = 0;

            await UpdatePlayer();
        }
        finally
        {
            lockObject.Release();
        }
    }

    private async ValueTask UpdatePlayer()
    {
        var playerName = playerNames[playerNameIndex];
        
        Services.PluginLog.Debug("Selecting D-Bus MPRIS service {PlayerName}", playerName);

        playerPropertyChangeObserver?.Dispose();
        
        mpris = new DBusService(dbus, playerName);
        player = mpris.Value.CreatePlayer("/org/mpris/MediaPlayer2");
        playerPropertyChangeObserver = await player.WatchPropertiesChangedAsync(OnPlayerPropertiesChanged);
        
        UpdateFromMetadata(await player.GetMetadataAsync());
        IsPaused = await player.GetPlaybackStatusAsync() == "Paused";
        OnUpdated?.Invoke(this, EventArgs.Empty);
    }

    private async ValueTask OnPlayerPropertiesChanged(IChangedPlayerProperties props)
    {
        if (props.HasPlaybackStatusChanged)
            IsPaused = props.PlaybackStatus == "Paused";
        
        if (props.HasMetadataChanged && (props.Metadata != null || player != null))
            UpdateFromMetadata(props.Metadata ?? await player!.GetMetadataAsync());
        
        OnUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateFromMetadata(Dictionary<string, VariantValue> metadata)
    {
        if (metadata.TryGetValue("xesam:title", out var title) && title.Type == VariantValueType.String)
            CurrentSong = title.GetString();
        else
            CurrentSong = string.Empty;

        if (metadata.TryGetValue("xesam:artist", out var artist) && artist.Type == VariantValueType.Array)
            CurrentArtist = string.Join(", ", artist.GetArray<string>());
        else
            CurrentArtist = string.Empty;
        
        if (metadata.TryGetValue("xesam:album", out var album) && album.Type == VariantValueType.String)
            CurrentAlbum = album.GetString();
        else
            CurrentAlbum = string.Empty;
    }
    
    public string CurrentSong { get; private set; } = string.Empty;
    public string CurrentArtist { get; private set; } = string.Empty;
    public string CurrentAlbum { get; private set; } = string.Empty;
    public bool IsPaused { get; private set; }
    
    public event EventHandler? OnUpdated;

    public bool TryPrevious()
    {
        if (player is not { } p)
            return false;

        p.PreviousAsync().Wait();
        return true;
    }

    public bool TryNext()
    {
        if (player is not { } p)
            return false;

        p.NextAsync().Wait();
        return true;
    }

    public bool TryPlay()
    {
        if (player is not { } p)
            return false;

        p.PlayAsync().Wait();
        return true;
    }

    public bool TryPause()
    {
        if (player is not { } p)
            return false;

        p.PauseAsync().Wait();
        return true;
    }

    public bool TryPlayPauseToggle()
    {
        if (player is not { } p)
            return false;

        p.PlayPauseAsync().Wait();
        return true;
    }

    public void CycleSession()
    {
        Task.Run(async () =>
        {
            await lockObject.WaitAsync();
            try
            {
                if (playerNames.Count == 0)
                    return;

                playerNameIndex++;

                if (playerNameIndex >= playerNames.Count)
                    playerNameIndex = 0;
                
                await UpdatePlayer();
            } 
            finally
            {
                lockObject.Release();
            }
        }).Wait();
    }
    
    
}
