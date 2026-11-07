using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Rtx5090Monitoring.UI.Tests.BuildVerification;

internal static class ReleaseBuildVerification
{
    public static void Run(string directory)
    {
        using var stream = File.OpenRead(Path.Combine(directory, "Rtx5090Monitoring.App.dll"));
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var definition = metadata.GetTypeDefinition(handle);
            var name = metadata.GetString(definition.Namespace);
            if (name.StartsWith("Rtx5090Monitoring.App.Debugging", StringComparison.Ordinal)
                || name.StartsWith("Rtx5090Monitoring.App.Demo", StringComparison.Ordinal)
                || name.StartsWith("Rtx5090Monitoring.App.Diagnostics", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Ordinary Release includes a simulation type: " + name);
            }
        }
        var methods = metadata.MethodDefinitions.Select(handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name)).ToArray();
        if (methods.Any(method => method is "OpenDebugConsole" or "PublishDebugFrame" or "InitializeDebugging" or "get_Debug"))
        {
            throw new InvalidOperationException("Ordinary Release includes a debug activation method.");
        }

        foreach (var handle in metadata.AssemblyReferences)
        {
            var name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
            if (name is "Rtx5090Monitoring.Debugging" or "Rtx5090Monitoring.Plugins.Asus.Analysis" or "Rtx5090Monitoring.Plugins.WireView.Analysis" or "Rtx5090Monitoring.Warnings")
            {
                throw new InvalidOperationException("Ordinary Release references a simulation dependency: " + name);
            }
        }
        if (Directory.EnumerateFiles(directory, "Rtx5090Monitoring.Debugging.*", SearchOption.AllDirectories).Any()
            || Directory.Exists(Path.Combine(directory, "Debugging")))
        {
            throw new InvalidOperationException("Ordinary Release includes simulation artifacts.");
        }

        Console.WriteLine("PASS: Ordinary Release contains no debug/demo/diagnostic types, debug activation methods, debugging references or debug XAML/artifacts.");
    }
}
