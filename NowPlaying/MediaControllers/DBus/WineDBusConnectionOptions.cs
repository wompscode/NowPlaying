using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace NowPlaying.MediaControllers.DBus;

internal class WineDBusConnectionOptions(string address) : DBusConnectionOptions
{
    protected override async ValueTask<SetupResult> SetupAsync(CancellationToken cancellationToken)
    {
        // There's gotta be a better way to obtain the UID of the currently running process.
        // $UID env var does not work, calling out to /bin/id or /bin/sh will give no stdout, so you can't
        // extract it from there.
        var status = await File.ReadAllLinesAsync("/proc/self/status", cancellationToken);

        foreach (var line in status)
        {
            if (!line.StartsWith("Uid:"))
                continue;
            
            var splits = line.Split('\t');

            return new SetupResult(address)
            {
                SupportsFdPassing = false,
                UserId = splits[2], // effective UID
                MachineId = Guid.Parse((await File.ReadAllTextAsync("/var/lib/dbus/machine-id", cancellationToken))
                                       .AsSpan(0, 32))
                                .ToString("N"),
            };
        }

        throw new Exception("Could not determine the UID of the process from /proc/self/status");
    }
}
