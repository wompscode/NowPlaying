# ![logo](logo_64.png) NowPlaying
Shows the currently playing song in your server info bar using [NPSMLib](https://www.nuget.org/packages/NPSMLib), and gives you commands to play/pause, skip and go back songs in the currently active player in Windows.  
&nbsp;  

### Limitations & Jank
This plugin is mostly complete, but is incredibly finicky and due to the way that Windows prioritises apps that use the SystemMediaTransportControl functionality, you might not be able to control/see the right media player output.  

Personally, I have no idea how to resolve this. Windows APIs kinda suck and if you know how to fix it (or know some incredibly hacky but functional workaround) and are willing to help - PRs are greatly appreciated.
  
The plugin also will receive multiple `PlaybackDataChanged` events, and this appears to just be how the API functions, and certain applications send more than others (I'm looking at you Spotify.) (I'm not complaining about this specifically though, I'm just nitpicky that everything implements SMTC in their own stupid way, and not in a consistent across apps way).  
  
~~Final thing to note: I am incredibly confident that this *will not work under Linux (or macOS) whatsoever.* I could be wrong, and WINE could handle the exact things this plugin needs, but I fully doubt that it does, and I'm not going to test it. If it doesn't work, don't ever expect it to. I'd like for things to be entirely functional across all platforms, but unfortunately it's just this way sometimes. Such is life.~~
WINE does not implement the required functions in `Windows.Media` for this to function. Fortunately, it errors out safely and Dalamud just doesn't load the plugin. I have tested this on my Steam Deck, however if this is an issue that is run into, I can probably just check if this is running in WINE and not initialise anything, so it'll just be useless and doing nothing. Again, such is life.   
  
If the required functionality ever gets implemented into WINE, you can just update whatever version of WINE you're using to have it, or if you require an older version - you can probably just add those patches in manually and compile it yourself.
&nbsp;
### Dependencies
`NPSMLib`: `0.9.14` ([nuget](https://www.nuget.org/packages/NPSMLib))  
&nbsp;

### Usage
It's on the Dalamud repository now! Currently testing exclusive, however this won't be forever.
  
### Compiling
It's pretty straight forward. Install `NPSMLib` from NuGet, and have a proper setup XIVLauncher/Dalamud environment. Compile away.
