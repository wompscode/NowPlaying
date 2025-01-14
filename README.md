# ![logo](logo_64.png) NowPlaying
Shows the currently playing song in your server info bar using [NPSMLib](https://www.nuget.org/packages/NPSMLib), and gives you commands to play/pause, skip and go back songs in the currently active player in Windows.  
&nbsp;  

### Limitations & Jank
This plugin is mostly complete, but is incredibly finicky and due to the way that Windows prioritises apps that use the SystemMediaTransportControl functionality, you might not be able to control/see the right media player output.  

Personally, I have no idea how to resolve this. Windows APIs kinda suck and if you know how to fix it (or know some incredibly hacky but functional workaround) and are willing to help - PRs are greatly appreciated.
  
The plugin also will receive multiple `PlaybackDataChanged` events, and this appears to just be how the API functions, and certain applications send more than others (I'm looking at you Spotify.).  
&nbsp;
### Dependencies
`NPSMLib`: `0.9.14` ([nuget](https://www.nuget.org/packages/NPSMLib))  
&nbsp;
  
### Compiling
This may or may not end up on the main Dalamud repository - so until then, compile it yourself.  Maybe eventually I'll spin my own repo up for whatever junk I make, so I don't have to submit anything and everything.  

It's pretty straight forward. Install `NPSMLib` from NuGet, and have a proper setup XIVLauncher/Dalamud environment. Compile away.
