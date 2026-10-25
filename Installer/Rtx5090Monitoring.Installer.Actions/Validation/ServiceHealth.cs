using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace Rtx5090Monitoring.Installer.Actions.Validation;

public static class ServiceHealth
{
    public static bool IsHealthy(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var health = (HealthResponse?)new DataContractJsonSerializer(typeof(HealthResponse)).ReadObject(stream);
        return health is { Status: "running", ConfigurationRevision: > 0 };
    }

    public static void Wait()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do
        {
            try
            {
                var request = WebRequest.CreateHttp("http://localhost:59273/health");
                request.Timeout = request.ReadWriteTimeout = 2000;
                request.Proxy = null!;
                using var response = request.GetResponse();
                using var reader = new StreamReader(response.GetResponseStream()!);
                if (IsHealthy(reader.ReadToEnd()))
                {
                    return;
                }
            }
            catch (Exception error) when (error is WebException or IOException or System.Runtime.Serialization.SerializationException)
            {
                // The service may still be initializing, no hardware needs to be reporting
            }

            Thread.Sleep(500);
        } while (DateTime.UtcNow < deadline);

        throw new IOException("RTX Monitor did not pass GET /health within 30 seconds. See the Application event log and installer log. Installation will roll back.");
    }
}
