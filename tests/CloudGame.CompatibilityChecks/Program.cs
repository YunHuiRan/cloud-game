using System;
using System.IO;
using System.Linq;
using Ludens;

internal static class Program
{
    private static int Main()
    {
        try
        {
            if (!File.Exists(AppPaths.DataFile))
            {
                Console.WriteLine("SKIP: No local Cloud Game library.json was found at " + AppPaths.DataFile);
                return 0;
            }

            string source = File.ReadAllText(AppPaths.DataFile);
            AppData original = JsonHelper.Deserialize<AppData>(source);
            if (original == null || original.Games == null || original.Settings == null)
                throw new InvalidDataException("The current library has no valid root data.");

            string roundTrip = JsonHelper.Serialize(original, true);
            AppData restored = JsonHelper.Deserialize<AppData>(roundTrip);
            if (restored == null || restored.Games == null || restored.Settings == null)
                throw new InvalidDataException("Round-trip deserialization produced invalid root data.");
            if (original.Games.Count != restored.Games.Count)
                throw new InvalidDataException("Game count changed during JSON round-trip.");

            for (int i = 0; i < original.Games.Count; i++)
            {
                GameEntry before = original.Games[i];
                GameEntry after = restored.Games[i];
                if (!string.Equals(before.Id, after.Id, StringComparison.Ordinal) ||
                    !string.Equals(before.Title, after.Title, StringComparison.Ordinal) ||
                    before.TotalSeconds != after.TotalSeconds ||
                    (before.Sessions == null ? 0 : before.Sessions.Count) != (after.Sessions == null ? 0 : after.Sessions.Count))
                {
                    throw new InvalidDataException("Game data changed during JSON round-trip: " + (before.Title ?? before.Id));
                }
            }

            Console.WriteLine("PASS: {0} game(s), {1} play session(s); existing library.json is compatible with .NET 8.",
                restored.Games.Count,
                restored.Games.Sum(delegate(GameEntry game) { return game.Sessions == null ? 0 : game.Sessions.Count; }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }
}