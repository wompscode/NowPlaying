using System;
using System.ComponentModel;

namespace NowPlaying.MediaControllers;

public interface IMediaController : IDisposable
{
    void Start();
    
    string CurrentSong { get; }
    string CurrentArtist { get; }
    string CurrentAlbum { get; }
    bool IsPaused { get; }

    event EventHandler OnUpdated;
    
    bool TryPrevious();
    bool TryNext();
    bool TryPlay();
    bool TryPause();
    bool TryPlayPauseToggle();
    void CycleSession();
}
