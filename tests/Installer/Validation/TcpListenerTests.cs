using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Rtx5090Monitoring.Installer.Actions.Validation;

namespace Rtx5090Monitoring.Installer.Tests.Validation;

internal static class TcpListenerTests
{
    public static void Run()
    {
        foreach (var address in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
        {
            if (address.AddressFamily == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
            {
                continue;
            }

            var listener = new TcpListener(address, 0);
            try
            {
                listener.Start();
                var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                Check.That(TcpListenerOwners.Read(port).Contains(Process.GetCurrentProcess().Id), "Native port conflict detection identifies the correct process on " + address.AddressFamily + ".");
            }
            finally { listener.Stop(); }
        }
    }
}
