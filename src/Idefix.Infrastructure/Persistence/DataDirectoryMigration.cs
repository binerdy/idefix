namespace Idefix.Infrastructure.Persistence;

public static class DataDirectoryMigration
{
    private static readonly string[] Files = ["profile.dat", "projects.json"];
    private static readonly string[] Folders = ["sessions", "logs"];

    public static IReadOnlyList<string> Migrate(string legacyDirectory, string dataDirectory)
    {
        var moved = new List<string>();
        if (!Directory.Exists(legacyDirectory)
            || string.Equals(Path.GetFullPath(legacyDirectory), Path.GetFullPath(dataDirectory), StringComparison.OrdinalIgnoreCase))
        {
            return moved;
        }

        Directory.CreateDirectory(dataDirectory);
        foreach (var name in Files)
        {
            var source = Path.Combine(legacyDirectory, name);
            var target = Path.Combine(dataDirectory, name);
            if (File.Exists(source) && !File.Exists(target))
            {
                File.Move(source, target);
                moved.Add(name);
            }
        }

        foreach (var name in Folders)
        {
            var source = Path.Combine(legacyDirectory, name);
            if (Directory.Exists(source) && MoveFolder(source, Path.Combine(dataDirectory, name)))
            {
                moved.Add(name);
            }
        }

        return moved;
    }

    private static bool MoveFolder(string source, string target)
    {
        if (!Directory.Exists(target))
        {
            Directory.Move(source, target);
            return true;
        }

        var any = false;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToList())
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            if (File.Exists(destination))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(file, destination);
            any = true;
        }

        return any;
    }
}
