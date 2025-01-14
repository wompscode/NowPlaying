using Dalamud.IoC;
using Dalamud.Plugin.Services;
using Dalamud.Plugin;

namespace NowPlaying;

// I used this for reference https://github.com/Haplo064/ChatBubbles/blob/main/ChatBubbles/Services.cs, and I'd feel awful if I never said that I did.
public class Services
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; }
    [PluginService] internal static IPluginLog PluginLog { get; private set; }
    [PluginService] internal static IChatGui ChatGui { get; private set; }
    [PluginService] internal static IClientState ClientState { get; private set; }
    [PluginService] internal static ICommandManager CommandManager { get; private set; }
    [PluginService] internal static IDtrBar DtrBar { get; private set; }
    [PluginService] internal static IFramework Framework { get; private set; }
    [PluginService] internal static IGameConfig GameConfig { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
