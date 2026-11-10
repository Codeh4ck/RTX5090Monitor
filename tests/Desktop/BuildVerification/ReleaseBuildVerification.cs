using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace RTXLens.UI.Tests.BuildVerification;

internal static class ReleaseBuildVerification
{
    public static void Run(string directory)
    {
        using var stream = File.OpenRead(Path.Combine(directory, "RTXLens.App.dll"));
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var definition = metadata.GetTypeDefinition(handle);
            var name = metadata.GetString(definition.Namespace);
            if (name.StartsWith("RTXLens.App.Debugging", StringComparison.Ordinal)
                || name.StartsWith("RTXLens.App.Demo", StringComparison.Ordinal)
                || name.StartsWith("RTXLens.App.Diagnostics", StringComparison.Ordinal))
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
            if (name is "RTXLens.Debugging" or "RTXLens.Plugins.Asus.Analysis" or "RTXLens.Plugins.WireView.Analysis" or "RTXLens.Warnings")
            {
                throw new InvalidOperationException("Ordinary Release references a simulation dependency: " + name);
            }
        }
        if (Directory.EnumerateFiles(directory, "RTXLens.Debugging.*", SearchOption.AllDirectories).Any()
            || Directory.Exists(Path.Combine(directory, "Debugging")))
        {
            throw new InvalidOperationException("Ordinary Release includes simulation artifacts.");
        }

        Console.WriteLine("PASS: Ordinary Release contains no debug/demo/diagnostic types, debug activation methods, debugging references or debug XAML/artifacts.");
    }
}
