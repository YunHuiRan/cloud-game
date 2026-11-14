using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ludens
{
    public class GameChangedEventArgs : EventArgs
    {
        public GameEntry Game { get; private set; }
        public GameChangedEventArgs(GameEntry game) { Game = game; }
    }

    public class StoreErrorEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public StoreErrorEventArgs(string message) { Message = message; }
    }

    public sealed class AppStore
    {
        private static readonly object SyncRoot = new object();
        private static AppStore _instance;
        private bool _suppressNotify;

        public static AppStore Instance
        {
            get
            {
                if (_instance == null) _instance = new AppStore();
                return _instance;
            }
        }

        public AppData Data { get; private set; }
        public List<GameEntry> Games { get { return Data.Games; } }
        public AppSettings Settings { get { return Data.Settings; } }

        public event EventHandler Changed;
        public event EventHandler<GameChangedEventArgs> GameChanged;
        public event EventHandler<StoreErrorEventArgs> StoreError;
        public event EventHandler LoadCompleted;

        private AppStore()
        {
            AppPaths.Ensure();
            Data = new AppData();
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(AppPaths.DataFile))
                {
                    string json = File.ReadAllText(AppPaths.DataFile, Encoding.UTF8);
                    AppData loaded = JsonHelper.Deserialize<AppData>(json);
                    Data = loaded ?? new AppData();
                }
                Normalize();
                Save(false);
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(AppPaths.DataFile))
                    {
                        string bad = AppPaths.DataFile + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        File.Copy(AppPaths.DataFile, bad, true);
                    }
                }
                catch { }

                Log(ex);
                Data = new AppData();
                Normalize();
                RaiseError("游戏库数据读取失败，已创建新的游戏库。原始文件已备份。");
            }

            if (LoadCompleted != null) LoadCompleted(this, EventArgs.Empty);
        }

        private void Normalize()
        {
            if (Data.Games == null) Data.Games = new List<GameEntry>();
            if (Data.Settings == null) Data.Settings = new AppSettings();
            if (string.IsNullOrEmpty(Data.Settings.Theme)) Data.Settings.Theme = "dark";
            if (string.IsNullOrEmpty(Data.Settings.AccentHex)) Data.Settings.AccentHex = "#FF5B3A";
            if (string.IsNullOrEmpty(Data.Settings.SortMode)) Data.Settings.SortMode = "recent";
            if (string.IsNullOrEmpty(Data.Settings.ViewMode)) Data.Settings.ViewMode = "grid";
            Data.Settings.Animations = true;
            if (Data.Settings.BackgroundDim < 0.05 || Data.Settings.BackgroundDim > 0.95)
                Data.Settings.BackgroundDim = 0.48;
            if (Data.Settings.WindowWidth < 1000) Data.Settings.WindowWidth = 1360;
            if (Data.Settings.WindowHeight < 640) Data.Settings.WindowHeight = 860;

            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<GameEntry> clean = new List<GameEntry>();
            for (int i = 0; i < Data.Games.Count; i++)
            {
                GameEntry g = Data.Games[i];
                if (g == null) continue;
                if (string.IsNullOrEmpty(g.Id) || ids.Contains(g.Id))
                    g.Id = Guid.NewGuid().ToString("N");
                ids.Add(g.Id);
                if (g.Sessions == null) g.Sessions = new List<PlaySession>();
                g.Sessions.RemoveAll(delegate(PlaySession s) { return s == null || s.Seconds < 0; });
                g.Sessions.Sort(delegate(PlaySession a, PlaySession b) { return b.StartUtc.CompareTo(a.StartUtc); });
                if (g.TotalSeconds < 0) g.TotalSeconds = 0;
                if (string.IsNullOrEmpty(g.Title))
                {
                    try { g.Title = Path.GetFileNameWithoutExtension(g.ExePath); }
                    catch { g.Title = "未命名游戏"; }
                }
                if (g.AddedUtc == default(DateTime)) g.AddedUtc = DateTime.UtcNow;
                clean.Add(g);
            }
            Data.Games = clean;
            for (int i = 0; i < Data.Games.Count; i++)
            {
                try { MediaService.EnsureArtwork(Data.Games[i]); }
                catch (Exception ex) { Log(ex); }
            }
        }

        public void Save()
        {
            Save(true);
        }

        public void Save(bool notify)
        {
            lock (SyncRoot)
            {
                try
                {
                    AppPaths.Ensure();
                    Data.UpdatedUtc = DateTime.UtcNow;
                    string json = JsonHelper.Serialize(Data, true);
                    string temp = AppPaths.DataFile + ".tmp";
                    File.WriteAllText(temp, json, new UTF8Encoding(false));
                    if (File.Exists(AppPaths.DataFile))
                    {
                        string backup = AppPaths.DataFile + ".bak";
                        try
                        {
                            File.Replace(temp, AppPaths.DataFile, backup, true);
                            try { File.Delete(backup); } catch { }
                        }
                        catch
                        {
                            File.Copy(temp, AppPaths.DataFile, true);
                            File.Delete(temp);
                        }
                    }
                    else
                    {
                        File.Move(temp, AppPaths.DataFile);
                    }
                }
                catch (Exception ex)
                {
                    Log(ex);
                    RaiseError("无法保存游戏库：" + ex.Message);
                    return;
                }
            }
            if (notify && !_suppressNotify) RaiseChanged();
        }

        public void SetSuppressed(bool suppressed)
        {
            _suppressNotify = suppressed;
        }

        public GameEntry FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Games.Count; i++)
            {
                if (string.Equals(Games[i].Id, id, StringComparison.OrdinalIgnoreCase)) return Games[i];
            }
            return null;
        }

        public void AddGame(GameEntry game)
        {
            if (game == null) return;
            if (string.IsNullOrEmpty(game.Id)) game.Id = Guid.NewGuid().ToString("N");
            if (game.AddedUtc == default(DateTime)) game.AddedUtc = DateTime.UtcNow;
            Games.Insert(0, game);
            Save();
        }

        public void UpdateGame(GameEntry game)
        {
            Save();
        }

        public void DeleteGame(GameEntry game)
        {
            if (game == null) return;
            Games.Remove(game);
            Save(false);
            try
            {
                string folder = AppPaths.GameMediaFolder(game.Id);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            catch (Exception ex) { Log(ex); }
            RaiseChanged();
        }

        public void ToggleFavorite(GameEntry game)
        {
            if (game == null) return;
            game.Favorite = !game.Favorite;
            Save(false);
            NotifyGame(game);
        }

        public IEnumerable<GameEntry> GetSorted(int filter, string search)
        {
            IEnumerable<GameEntry> query = Games;
            if (filter == 1)
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-30);
                query = query.Where(g => g.LastPlayedUtc.HasValue && g.LastPlayedUtc.Value >= cutoff);
            }
            else if (filter == 2)
            {
                query = query.Where(g => g.Favorite);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim().ToLowerInvariant();
                query = query.Where(g =>
                    Contains(g.Title, q) || Contains(g.Developer, q) || Contains(g.Genre, q));
            }

            List<GameEntry> list = query.ToList();
            string mode = Settings.SortMode;
            if (mode == "title")
            {
                list.Sort(delegate(GameEntry a, GameEntry b)
                {
                    return string.Compare(a.Title ?? "", b.Title ?? "", CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
                });
            }
            else if (mode == "playtime")
            {
                list.Sort(delegate(GameEntry a, GameEntry b) { return b.DisplaySeconds.CompareTo(a.DisplaySeconds); });
            }
            else if (mode == "added")
            {
                list.Sort(delegate(GameEntry a, GameEntry b) { return b.AddedUtc.CompareTo(a.AddedUtc); });
            }
            else
            {
                list.Sort(delegate(GameEntry a, GameEntry b)
                {
                    DateTime da = a.LastPlayedUtc ?? a.AddedUtc;
                    DateTime db = b.LastPlayedUtc ?? b.AddedUtc;
                    return db.CompareTo(da);
                });
            }
            return list;
        }

        private static bool Contains(string source, string query)
        {
            return !string.IsNullOrEmpty(source) && source.ToLowerInvariant().Contains(query);
        }

        public List<GameEntry> GetRecent(int count)
        {
            return Games
                .Where(g => g.LastPlayedUtc.HasValue)
                .OrderByDescending(g => g.LastPlayedUtc.Value)
                .Take(count)
                .ToList();
        }

        public long TotalPlaySeconds
        {
            get
            {
                long total = 0;
                for (int i = 0; i < Games.Count; i++) total += Games[i].DisplaySeconds;
                return total;
            }
        }

        public long WeekPlaySeconds
        {
            get
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-7);
                long total = 0;
                for (int i = 0; i < Games.Count; i++)
                {
                    for (int j = 0; j < Games[i].Sessions.Count; j++)
                    {
                        PlaySession s = Games[i].Sessions[j];
                        if (s.StartUtc >= cutoff) total += s.Seconds;
                    }
                }
                return total;
            }
        }

        public string MostPlayedTitle
        {
            get
            {
                GameEntry g = Games.OrderByDescending(x => x.TotalSeconds).FirstOrDefault();
                return g == null ? "—" : g.Title;
            }
        }

        public void AddSession(GameEntry game, DateTime startUtc, DateTime endUtc, long seconds)
        {
            if (game == null) return;
            if (game.Sessions == null) game.Sessions = new List<PlaySession>();
            if (seconds < 1) seconds = 1;
            game.Sessions.Add(new PlaySession { StartUtc = startUtc, EndUtc = endUtc, Seconds = seconds });
            game.Sessions.Sort(delegate(PlaySession a, PlaySession b) { return b.StartUtc.CompareTo(a.StartUtc); });
            if (game.Sessions.Count > 200) game.Sessions.RemoveRange(200, game.Sessions.Count - 200);
            game.TotalSeconds += seconds;
            game.LastPlayedUtc = endUtc;
            game.ActiveSessionStartUtc = null;
            game.ActiveSessionLastSeenUtc = null;
        }

        public void BeginActiveSession(GameEntry game, DateTime startUtc)
        {
            game.ActiveSessionStartUtc = startUtc;
            game.ActiveSessionLastSeenUtc = startUtc;
            game.LastPlayedUtc = startUtc;
            game.LaunchCount++;
            Save(false);
        }

        public void Heartbeat(GameEntry game, DateTime seenUtc)
        {
            game.ActiveSessionLastSeenUtc = seenUtc;
            Save(false);
        }

        public void FinishActiveSession(GameEntry game, DateTime endUtc)
        {
            if (game.ActiveSessionStartUtc.HasValue)
            {
                DateTime start = game.ActiveSessionStartUtc.Value;
                if (endUtc < start) endUtc = start;
                long seconds = (long)Math.Round((endUtc - start).TotalSeconds);
                AddSession(game, start, endUtc, seconds);
            }
            else
            {
                game.ActiveSessionStartUtc = null;
                game.ActiveSessionLastSeenUtc = null;
            }
            Save(false);
        }

        public void RecoverOrphan(GameEntry game)
        {
            if (game == null || !game.ActiveSessionStartUtc.HasValue) return;
            DateTime start = game.ActiveSessionStartUtc.Value;
            DateTime end = game.ActiveSessionLastSeenUtc ?? start;
            if (end < start) end = start;
            long seconds = (long)Math.Round((end - start).TotalSeconds);
            if (seconds > 0)
            {
                AddSession(game, start, end, seconds);
            }
            else
            {
                game.ActiveSessionStartUtc = null;
                game.ActiveSessionLastSeenUtc = null;
            }
        }

        public ExportPackage BuildExportPackage(bool includeMedia)
        {
            ExportPackage package = new ExportPackage();
            package.Kind = includeMedia ? "full" : "play";
            package.ExportedUtc = DateTime.UtcNow;
            for (int i = 0; i < Games.Count; i++)
            {
                GameEntry g = Games[i];
                ExportGame eg = new ExportGame();
                eg.Id = g.Id;
                eg.Title = g.Title;
                eg.ExePath = g.ExePath;
                eg.Arguments = g.Arguments;
                eg.WorkingDirectory = g.WorkingDirectory;
                eg.Description = g.Description;
                eg.Developer = g.Developer;
                eg.Genre = g.Genre;
                eg.ReleaseYear = g.ReleaseYear;
                eg.Favorite = g.Favorite;
                eg.AddedUtc = g.AddedUtc;
                eg.LastPlayedUtc = g.LastPlayedUtc;
                eg.TotalSeconds = g.DisplaySeconds;
                eg.LaunchCount = g.LaunchCount;
                eg.ActiveSessionStartUtc = g.ActiveSessionStartUtc;
                eg.ActiveSessionLastSeenUtc = g.ActiveSessionLastSeenUtc;
                for (int j = 0; j < g.Sessions.Count; j++)
                {
                    PlaySession s = g.Sessions[j];
                    eg.Sessions.Add(new PlaySession { StartUtc = s.StartUtc, EndUtc = s.EndUtc, Seconds = s.Seconds });
                }

                if (includeMedia)
                {
                    eg.CoverData = ReadImageBase64(g.CoverAbsolutePath);
                    eg.BackgroundData = ReadImageBase64(g.BackgroundAbsolutePath);
                    eg.LogoData = ReadImageBase64(g.LogoAbsolutePath);
                }
                package.Games.Add(eg);
            }
            return package;
        }

        public long ExportToFile(string path, bool includeMedia)
        {
            ExportPackage package = BuildExportPackage(includeMedia);
            string json = JsonHelper.Serialize(package, true);
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return new FileInfo(path).Length;
        }

        public ImportResult ImportFromFile(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            ExportPackage package;
            try
            {
                package = JsonHelper.Deserialize<ExportPackage>(json);
            }
            catch
            {
                throw new InvalidDataException("这不是有效的 Cloud Game 数据文件。");
            }

            if (package == null || package.Games == null)
                throw new InvalidDataException("数据文件中没有游戏信息。");
            if (!string.IsNullOrEmpty(package.Format) && package.Format != "cloudgame.library" && package.Format != "ludens.library")
                throw new InvalidDataException("数据格式无法识别：" + package.Format);

            ImportResult result = new ImportResult();
            bool hasMedia = false;
            for (int i = 0; i < package.Games.Count; i++)
            {
                ExportGame eg = package.Games[i];
                if (eg == null) { result.Skipped++; continue; }
                if (string.IsNullOrEmpty(eg.Title) && string.IsNullOrEmpty(eg.ExePath))
                {
                    result.Skipped++;
                    continue;
                }

                GameEntry existing = FindImportMatch(eg);
                if (existing != null)
                {
                    result.Sessions += MergeExportGame(existing, eg, ref hasMedia);
                    result.Merged++;
                }
                else
                {
                    GameEntry created = CreateFromExport(eg, ref hasMedia);
                    Games.Insert(0, created);
                    result.Sessions += created.Sessions.Count;
                    result.Added++;
                }
            }

            Save(false);
            for (int i = 0; i < Games.Count; i++) MediaService.EnsureArtwork(Games[i]);
            Save(false);
            RaiseChanged();
            return result;
        }

        private GameEntry FindImportMatch(ExportGame eg)
        {
            if (!string.IsNullOrEmpty(eg.Id))
            {
                GameEntry byId = FindById(eg.Id);
                if (byId != null) return byId;
            }
            if (!string.IsNullOrEmpty(eg.ExePath))
            {
                string exe = eg.ExePath.Trim();
                for (int i = 0; i < Games.Count; i++)
                {
                    if (string.Equals((Games[i].ExePath ?? "").Trim(), exe, StringComparison.OrdinalIgnoreCase))
                        return Games[i];
                }
            }
            if (!string.IsNullOrEmpty(eg.Title))
            {
                string title = eg.Title.Trim();
                for (int i = 0; i < Games.Count; i++)
                {
                    if (string.Equals((Games[i].Title ?? "").Trim(), title, StringComparison.OrdinalIgnoreCase))
                        return Games[i];
                }
            }
            return null;
        }

        private int MergeExportGame(GameEntry game, ExportGame eg, ref bool hasMedia)
        {
            if (game.Sessions == null) game.Sessions = new List<PlaySession>();
            int addedSessions = 0;

            for (int i = 0; i < eg.Sessions.Count; i++)
            {
                PlaySession incoming = eg.Sessions[i];
                if (incoming == null || incoming.Seconds < 0) continue;
                PlaySession same = game.Sessions.FirstOrDefault(s => Math.Abs((s.StartUtc - incoming.StartUtc).TotalSeconds) < 1);
                if (same == null)
                {
                    game.Sessions.Add(new PlaySession
                    {
                        StartUtc = incoming.StartUtc,
                        EndUtc = incoming.EndUtc,
                        Seconds = incoming.Seconds
                    });
                    game.TotalSeconds += incoming.Seconds;
                    addedSessions++;
                }
                else if (incoming.Seconds > same.Seconds)
                {
                    game.TotalSeconds += incoming.Seconds - same.Seconds;
                    same.Seconds = incoming.Seconds;
                    same.EndUtc = incoming.EndUtc;
                }
            }

            if (eg.ActiveSessionStartUtc.HasValue)
            {
                DateTime start = eg.ActiveSessionStartUtc.Value;
                DateTime end = eg.ActiveSessionLastSeenUtc ?? start;
                long seconds = (long)Math.Round((end - start).TotalSeconds);
                if (seconds > 0)
                {
                    bool exists = game.Sessions.Any(s => Math.Abs((s.StartUtc - start).TotalSeconds) < 1);
                    if (!exists)
                    {
                        game.Sessions.Add(new PlaySession { StartUtc = start, EndUtc = end, Seconds = seconds });
                        game.TotalSeconds += seconds;
                        addedSessions++;
                    }
                }
            }

            if (eg.TotalSeconds > game.TotalSeconds) game.TotalSeconds = eg.TotalSeconds;
            game.LaunchCount = Math.Max(game.LaunchCount, eg.LaunchCount);
            if (eg.LastPlayedUtc.HasValue && (!game.LastPlayedUtc.HasValue || eg.LastPlayedUtc.Value > game.LastPlayedUtc.Value))
                game.LastPlayedUtc = eg.LastPlayedUtc;
            game.Favorite = game.Favorite || eg.Favorite;
            if (string.IsNullOrEmpty(game.Description)) game.Description = eg.Description;
            if (string.IsNullOrEmpty(game.Developer)) game.Developer = eg.Developer;
            if (string.IsNullOrEmpty(game.Genre)) game.Genre = eg.Genre;
            if (string.IsNullOrEmpty(game.ReleaseYear)) game.ReleaseYear = eg.ReleaseYear;
            if (string.IsNullOrEmpty(game.ExePath)) game.ExePath = eg.ExePath;
            game.Sessions.Sort(delegate(PlaySession a, PlaySession b) { return b.StartUtc.CompareTo(a.StartUtc); });
            if (game.Sessions.Count > 200) game.Sessions.RemoveRange(200, game.Sessions.Count - 200);

            if (!string.IsNullOrEmpty(eg.CoverData) && string.IsNullOrEmpty(game.CoverImage))
            {
                game.CoverImage = SaveImportedImage(game.Id, "cover.jpg", DecodeImage(eg.CoverData));
                game.CoverGenerated = false;
                hasMedia = true;
            }
            if (!string.IsNullOrEmpty(eg.BackgroundData) && string.IsNullOrEmpty(game.BackgroundImage))
            {
                game.BackgroundImage = SaveImportedImage(game.Id, "background.jpg", DecodeImage(eg.BackgroundData));
                game.BackgroundGenerated = false;
                hasMedia = true;
            }
            if (!string.IsNullOrEmpty(eg.LogoData) && string.IsNullOrEmpty(game.LogoImage))
            {
                game.LogoImage = SaveImportedImage(game.Id, "logo.png", DecodeImage(eg.LogoData));
                hasMedia = true;
            }
            return addedSessions;
        }

        private GameEntry CreateFromExport(ExportGame eg, ref bool hasMedia)
        {
            GameEntry game = new GameEntry();
            game.Id = string.IsNullOrEmpty(eg.Id) || FindById(eg.Id) != null
                ? Guid.NewGuid().ToString("N")
                : eg.Id;
            game.Title = string.IsNullOrEmpty(eg.Title) ? "未命名游戏" : eg.Title.Trim();
            game.ExePath = eg.ExePath ?? "";
            game.Arguments = eg.Arguments ?? "";
            game.WorkingDirectory = eg.WorkingDirectory ?? "";
            game.Description = eg.Description ?? "";
            game.Developer = eg.Developer ?? "";
            game.Genre = eg.Genre ?? "";
            game.ReleaseYear = eg.ReleaseYear ?? "";
            game.Favorite = eg.Favorite;
            game.AddedUtc = eg.AddedUtc == default(DateTime) ? DateTime.UtcNow : eg.AddedUtc;
            game.LastPlayedUtc = eg.LastPlayedUtc;
            game.LaunchCount = eg.LaunchCount;
            game.TotalSeconds = 0;

            if (eg.Sessions != null)
            {
                for (int i = 0; i < eg.Sessions.Count; i++)
                {
                    PlaySession s = eg.Sessions[i];
                    if (s == null || s.Seconds < 0) continue;
                    game.Sessions.Add(new PlaySession { StartUtc = s.StartUtc, EndUtc = s.EndUtc, Seconds = s.Seconds });
                    game.TotalSeconds += s.Seconds;
                }
            }
            if (eg.ActiveSessionStartUtc.HasValue)
            {
                DateTime start = eg.ActiveSessionStartUtc.Value;
                DateTime end = eg.ActiveSessionLastSeenUtc ?? start;
                long seconds = (long)Math.Round((end - start).TotalSeconds);
                if (seconds > 0)
                {
                    game.Sessions.Add(new PlaySession { StartUtc = start, EndUtc = end, Seconds = seconds });
                    game.TotalSeconds += seconds;
                }
            }
            if (eg.TotalSeconds > game.TotalSeconds) game.TotalSeconds = eg.TotalSeconds;
            game.Sessions.Sort(delegate(PlaySession a, PlaySession b) { return b.StartUtc.CompareTo(a.StartUtc); });
            if (game.Sessions.Count > 200) game.Sessions.RemoveRange(200, game.Sessions.Count - 200);
            if (game.LastPlayedUtc == null && game.Sessions.Count > 0) game.LastPlayedUtc = game.Sessions[0].EndUtc;

            if (!string.IsNullOrEmpty(eg.CoverData))
            {
                game.CoverImage = SaveImportedImage(game.Id, "cover.jpg", DecodeImage(eg.CoverData));
                game.CoverGenerated = false;
                hasMedia = true;
            }
            if (!string.IsNullOrEmpty(eg.BackgroundData))
            {
                game.BackgroundImage = SaveImportedImage(game.Id, "background.jpg", DecodeImage(eg.BackgroundData));
                game.BackgroundGenerated = false;
                hasMedia = true;
            }
            if (!string.IsNullOrEmpty(eg.LogoData))
            {
                game.LogoImage = SaveImportedImage(game.Id, "logo.png", DecodeImage(eg.LogoData));
                hasMedia = true;
            }
            return game;
        }

        private static string SaveImportedImage(string gameId, string fileName, byte[] data)
        {
            try
            {
                string folder = AppPaths.GameMediaFolder(gameId);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, fileName);
                File.WriteAllBytes(path, data);
                return AppPaths.ToStoredPath(path);
            }
            catch (Exception ex)
            {
                Log(ex);
                return "";
            }
        }

        private static string ReadImageBase64(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return Convert.ToBase64String(File.ReadAllBytes(path));
            }
            catch (Exception ex) { Log(ex); }
            return null;
        }

        private static byte[] DecodeImage(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;
            try
            {
                return Convert.FromBase64String(data);
            }
            catch (Exception ex)
            {
                Log(ex);
                return null;
            }
        }

        public static void Log(Exception ex)
        {
            try
            {
                AppPaths.Ensure();
                File.AppendAllText(AppPaths.LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" +
                    (ex == null ? "Unknown" : ex.ToString()) + "\r\n\r\n", Encoding.UTF8);
            }
            catch { }
        }

        private void RaiseChanged()
        {
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        private void RaiseError(string message)
        {
            if (StoreError != null) StoreError(this, new StoreErrorEventArgs(message));
        }

        public void NotifyGame(GameEntry game)
        {
            if (GameChanged != null) GameChanged(this, new GameChangedEventArgs(game));
        }
    }
}
