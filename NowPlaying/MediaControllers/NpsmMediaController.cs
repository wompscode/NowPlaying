using System;
using System.Threading;
using NPSMLib;

namespace NowPlaying.MediaControllers;

public class NpsmMediaController : IMediaController
{
    private readonly NowPlayingSessionManager manager;
    private NowPlayingSession? session;
    private MediaPlaybackDataSource? src;
    public NowPlayingSession[] Sessions = [];
    public int SessionIndex;
    
    private bool isAttached;
    private readonly Lock lockObject = new();
    
    public NpsmMediaController()
    {
        manager = new NowPlayingSessionManager();
    }

    public void Start()
    {
        manager.SessionListChanged += OnSessionListChanged;
        OnSessionListChanged(null, null);
    }
    
    public void Dispose()
    {
        manager.SessionListChanged -= OnSessionListChanged;

        try
        {
            if (src != null && isAttached)
                src.MediaPlaybackDataChanged -= PlaybackDataChanged;
        }
        catch  (Exception e)
        {
            // might not be the same source as it was before so if we try to unhook, it'll get upset but it largely can be ignored. i dont care. it works.
            Services.PluginLog.Warning("Issue with unhooking Src.MediaPlaybackDataChanged, this error can likely be ignored as the playback source just likely was closed (error: {0}).", e.Message);
        }
        
        GC.SuppressFinalize(this);
    }

    private void OnSessionListChanged(object? sender, NowPlayingSessionManagerEventArgs? args)
    {
        OnUpdated?.Invoke(this, EventArgs.Empty);

        Sessions = manager.GetSessions();

        if (Sessions.Length <= 0)
            return;
        
        if (SessionIndex >= Sessions.Length)
            SessionIndex = 0;

        session = Sessions[SessionIndex];
        Services.PluginLog.Debug("Session is set.");
        
        src = session.ActivateMediaPlaybackDataSource();
        Services.PluginLog.Debug("Src is set.");
        
        if (src != null)
        {
            if (isAttached) return;
            
            src.MediaPlaybackDataChanged += PlaybackDataChanged;
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
        if (session != null)
        {
            lock (lockObject)
            {
                if (src != null)
                {
                    var mediaDetails = src.GetMediaObjectInfo();
                    var mediaPlaybackInfo = src.GetMediaPlaybackInfo();
                    
                    CurrentArtist = mediaDetails.Artist;
                    CurrentSong = mediaDetails.Title;
                    CurrentAlbum = mediaDetails.AlbumTitle;
                    IsPaused = mediaPlaybackInfo.PlaybackState == MediaPlaybackState.Paused;
                    OnUpdated?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        else
        {
            Services.PluginLog.Verbose("Session is null, so assume player shut.");
            CurrentArtist = "";
            CurrentSong = "";
            OnUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    public string CurrentSong { get; private set; } = string.Empty;
    public string CurrentArtist { get; private set; } = string.Empty;
    public string CurrentAlbum { get; private set; } = string.Empty;
    public bool IsPaused { get; private set; }
    
    public event EventHandler? OnUpdated;

    private bool TrySendMediaPlaybackCommand(MediaPlaybackCommands command)
    {
        if (src == null)
            return false;

        src.SendMediaPlaybackCommand(command);
        return true;
    }
    
    public bool TryPrevious()
    {
        return TrySendMediaPlaybackCommand(MediaPlaybackCommands.Previous);
    }

    public bool TryNext()
    {
        return TrySendMediaPlaybackCommand(MediaPlaybackCommands.Next);
    }

    public bool TryPlay()
    {
        return TrySendMediaPlaybackCommand(MediaPlaybackCommands.Play);
    }

    public bool TryPause()
    {
        return TrySendMediaPlaybackCommand(MediaPlaybackCommands.Pause);
    }

    public bool TryPlayPauseToggle()
    {
        return TrySendMediaPlaybackCommand(MediaPlaybackCommands.PlayPauseToggle);
    }

    public void CycleSession()
    {
        if (Sessions.Length == 0)
            return;
        
        SessionIndex += 1;

        if (SessionIndex >= Sessions.Length)
            SessionIndex = 0;

        if (src != null)
        {
            try
            {
                src.MediaPlaybackDataChanged -= PlaybackDataChanged;
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
}
