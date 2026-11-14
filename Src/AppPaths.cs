using System;
using System.IO;

namespace Ludens
{
    public static class AppPaths
    {
        static AppPaths()
        {
            DataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CloudGame");
            MediaRoot = Path.Combine(DataRoot, "media");
            DataFile = Path.Combine(DataRoot, "library.json");
            LogFile = Path.Combine(DataRoot, "error.log");
        }

        public static string DataRoot { get; private set; }
        public static string MediaRoot { get; private set; }
        public static string DataFile { get; private set; }
        public static string LogFile { get; private set; }

        public static void Ensure()
        {
            Directory.CreateDirectory(DataRoot);
            Directory.CreateDirectory(MediaRoot);
        }

        public static string Resolve(string storedPath)
        {
            if (string.IsNullOrEmpty(storedPath)) return "";
            try
            {
                if (Path.IsPathRooted(storedPath)) return storedPath;
                return Path.GetFullPath(Path.Combine(DataRoot, storedPath.Replace('/', '\\')));
            }
            catch
            {
                return "";
            }
        }

        public static string ToStoredPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return "";
            try
            {
                string full = Path.GetFullPath(absolutePath);
                string root = DataRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    return full.Substring(root.Length).Replace('\\', '/');
                }
                return full;
            }
            catch
            {
                return absolutePath;
            }
        }

        public static string GameMediaFolder(string gameId)
        {
            return Path.Combine(MediaRoot, gameId);
        }

        public static string AppMediaFolder
        {
            get { return Path.Combine(MediaRoot, "_app"); }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#") + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.##") + " MB";
        }
    }
}
