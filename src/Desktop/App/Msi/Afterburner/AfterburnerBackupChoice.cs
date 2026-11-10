namespace RTXLens.App.Msi.Afterburner;

public sealed record AfterburnerBackupChoice(string Id, string DateTimeText, string Name)
{
    public string Label => DateTimeText + " - " + Name;
}