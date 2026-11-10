namespace RTXLens.App.Services;

internal static class LegacyDesktopPreferences
{
    public static void Import(string directory)
    {
        var previous = Path.Combine(Path.GetDirectoryName(directory)!, "Rtx5090Monitoring");
        if (!Directory.Exists(previous))
        {
            return;
        }

        Directory.CreateDirectory(directory);

        foreach (var name in new[] { "preferences.json", "updates.json", "afterburner-installation.txt" })
        {
            var source = Path.Combine(previous, name);
            var destination = Path.Combine(directory, name);
            if (!File.Exists(source) || File.Exists(destination))
            {
                continue;
            }

            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                File.Copy(source, temporary);
                File.Move(temporary, destination, overwrite: false);
            }
            catch (IOException) when (File.Exists(destination))
            {
                // Another launch already imported or saved this file.
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }
}
