using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;

namespace Ludens
{
    [DataContract]
    public class PlaySession
    {
        [DataMember] public DateTime StartUtc { get; set; }
        [DataMember] public DateTime EndUtc { get; set; }
        [DataMember] public long Seconds { get; set; }

        [IgnoreDataMember]
        public string DateDisplay
        {
            get
            {
                DateTime local = StartUtc.ToLocalTime();
                DateTime today = DateTime.Today;
                if (local.Date == today) return "今天";
                if (local.Date == today.AddDays(-1)) return "昨天";
                if (local.Year == today.Year) return local.ToString("M月d日");
                return local.ToString("yyyy.MM.dd");
            }
        }

        [IgnoreDataMember]
        public string TimeDisplay
        {
            get
            {
                DateTime local = StartUtc.ToLocalTime();
                return local.ToString("HH:mm") + " — " + EndUtc.ToLocalTime().ToString("HH:mm");
            }
        }

        [IgnoreDataMember]
        public string DurationDisplay
        {
            get { return GameEntry.FormatDuration(Seconds); }
        }
    }

    [DataContract]
    public class GameEntry : INotifyPropertyChanged
    {
        private long _totalSeconds;
        private bool _isRunning;
        private double _liveSeconds;
        private DateTime? _lastPlayedUtc;
        private int _launchCount;
        private string _title;
        private string _exePath;
        private string _coverImage;
        private string _backgroundImage;
        private bool _coverGenerated;
        private bool _backgroundGenerated;
        private bool _favorite;

        [DataMember] public string Id { get; set; }

        [DataMember]
        public string Title
        {
            get { return _title; }
            set
            {
                if (_title != value)
                {
                    _title = value;
                    Raise("Title");
                    Raise("Initial");
                }
            }
        }

        [DataMember]
        public string ExePath
        {
            get { return _exePath; }
            set
            {
                if (_exePath != value)
                {
                    _exePath = value;
                    Raise("ExePath");
                    Raise("ExeFileName");
                }
            }
        }
        [DataMember] public string Arguments { get; set; }
        [DataMember] public string WorkingDirectory { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public string Developer { get; set; }
        [DataMember] public string Genre { get; set; }
        [DataMember] public string ReleaseYear { get; set; }
        [DataMember]
        public string CoverImage
        {
            get { return _coverImage; }
            set
            {
                if (_coverImage != value)
                {
                    _coverImage = value;
                    Raise("CoverImage");
                    Raise("CoverAbsolutePath");
                }
            }
        }

        [DataMember]
        public string BackgroundImage
        {
            get { return _backgroundImage; }
            set
            {
                if (_backgroundImage != value)
                {
                    _backgroundImage = value;
                    Raise("BackgroundImage");
                    Raise("BackgroundAbsolutePath");
                }
            }
        }

        [DataMember] public string LogoImage { get; set; }

        [DataMember]
        public bool CoverGenerated
        {
            get { return _coverGenerated; }
            set
            {
                if (_coverGenerated != value)
                {
                    _coverGenerated = value;
                    Raise("CoverGenerated");
                }
            }
        }

        [DataMember]
        public bool BackgroundGenerated
        {
            get { return _backgroundGenerated; }
            set
            {
                if (_backgroundGenerated != value)
                {
                    _backgroundGenerated = value;
                    Raise("BackgroundGenerated");
                }
            }
        }
        [DataMember]
        public bool Favorite
        {
            get { return _favorite; }
            set
            {
                if (_favorite != value)
                {
                    _favorite = value;
                    Raise("Favorite");
                }
            }
        }
        [DataMember] public DateTime AddedUtc { get; set; }

        [DataMember]
        public DateTime? LastPlayedUtc
        {
            get { return _lastPlayedUtc; }
            set
            {
                if (_lastPlayedUtc != value)
                {
                    _lastPlayedUtc = value;
                    Raise("LastPlayedUtc");
                    Raise("LastPlayedDisplay");
                }
            }
        }

        [DataMember]
        public long TotalSeconds
        {
            get { return _totalSeconds; }
            set
            {
                if (_totalSeconds != value)
                {
                    _totalSeconds = value;
                    Raise("TotalSeconds");
                    Raise("DisplaySeconds");
                    Raise("PlaytimeDisplay");
                    Raise("PlaytimeLongDisplay");
                }
            }
        }

        [DataMember]
        public int LaunchCount
        {
            get { return _launchCount; }
            set
            {
                if (_launchCount != value)
                {
                    _launchCount = value;
                    Raise("LaunchCount");
                }
            }
        }
        [DataMember] public List<PlaySession> Sessions { get; set; }
        [DataMember] public DateTime? ActiveSessionStartUtc { get; set; }
        [DataMember] public DateTime? ActiveSessionLastSeenUtc { get; set; }

        [IgnoreDataMember]
        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                if (_isRunning != value)
                {
                    _isRunning = value;
                    Raise("IsRunning");
                }
            }
        }

        [IgnoreDataMember]
        public double LiveSeconds
        {
            get { return _liveSeconds; }
            set
            {
                if (Math.Abs(_liveSeconds - value) > 0.01)
                {
                    _liveSeconds = value;
                    Raise("LiveSeconds");
                    Raise("DisplaySeconds");
                    Raise("PlaytimeDisplay");
                    Raise("PlaytimeLongDisplay");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        public GameEntry()
        {
            Sessions = new List<PlaySession>();
            AddedUtc = DateTime.UtcNow;
            TotalSeconds = 0;
            LaunchCount = 0;
        }

        [IgnoreDataMember]
        public long DisplaySeconds
        {
            get { return TotalSeconds + (long)Math.Max(0, LiveSeconds); }
        }

        [IgnoreDataMember]
        public string CoverAbsolutePath
        {
            get { return AppPaths.Resolve(CoverImage); }
        }

        [IgnoreDataMember]
        public string BackgroundAbsolutePath
        {
            get { return AppPaths.Resolve(BackgroundImage); }
        }

        [IgnoreDataMember]
        public string LogoAbsolutePath
        {
            get { return AppPaths.Resolve(LogoImage); }
        }

        [IgnoreDataMember]
        public string Initial
        {
            get
            {
                if (string.IsNullOrEmpty(Title)) return "?";
                string trimmed = Title.Trim();
                if (trimmed.Length == 0) return "?";
                return trimmed.Substring(0, 1).ToUpperInvariant();
            }
        }

        [IgnoreDataMember]
        public string ExeFileName
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(ExePath)) return "";
                    return Path.GetFileName(ExePath);
                }
                catch
                {
                    return "";
                }
            }
        }

        [IgnoreDataMember]
        public string LastPlayedDisplay
        {
            get
            {
                if (!LastPlayedUtc.HasValue) return "尚未游玩";
                DateTime local = LastPlayedUtc.Value.ToLocalTime();
                TimeSpan delta = DateTime.Now - local;
                if (delta.TotalMinutes < 1) return "刚刚";
                if (delta.TotalHours < 1) return string.Format("{0} 分钟前", (int)delta.TotalMinutes);
                if (delta.TotalDays < 1) return string.Format("{0} 小时前", (int)delta.TotalHours);
                if (delta.TotalDays < 7) return string.Format("{0} 天前", (int)delta.TotalDays);
                return local.ToString("yyyy.MM.dd");
            }
        }

        [IgnoreDataMember]
        public string PlaytimeDisplay
        {
            get { return FormatDuration(DisplaySeconds); }
        }

        [IgnoreDataMember]
        public string PlaytimeLongDisplay
        {
            get
            {
                long seconds = DisplaySeconds;
                if (seconds <= 0) return "0 分钟";
                long hours = seconds / 3600;
                long minutes = (seconds % 3600) / 60;
                if (hours <= 0)
                {
                    if (minutes <= 0) return "不到 1 分钟";
                    return string.Format("{0} 分钟", minutes);
                }
                return string.Format("{0} 小时 {1} 分", hours, minutes);
            }
        }

        [IgnoreDataMember]
        public string MetaLine
        {
            get
            {
                List<string> bits = new List<string>();
                if (!string.IsNullOrEmpty(Genre)) bits.Add(Genre);
                if (!string.IsNullOrEmpty(ReleaseYear)) bits.Add(ReleaseYear);
                if (!string.IsNullOrEmpty(Developer)) bits.Add(Developer);
                return string.Join("  ·  ", bits.ToArray());
            }
        }

        public static string FormatDuration(long seconds)
        {
            if (seconds <= 0) return "0 分钟";
            long hours = seconds / 3600;
            long minutes = (seconds % 3600) / 60;
            if (hours <= 0)
            {
                if (minutes <= 0) return "不到 1 分钟";
                return string.Format("{0} 分钟", minutes);
            }
            if (minutes <= 0) return string.Format("{0} 小时", hours);
            return string.Format("{0} 小时 {1} 分", hours, minutes);
        }

        public GameEntry ShallowCopy()
        {
            GameEntry g = (GameEntry)MemberwiseClone();
            g.Sessions = new List<PlaySession>();
            for (int i = 0; i < Sessions.Count; i++)
            {
                PlaySession s = Sessions[i];
                g.Sessions.Add(new PlaySession { StartUtc = s.StartUtc, EndUtc = s.EndUtc, Seconds = s.Seconds });
            }
            return g;
        }
    }

    [DataContract]
    public class AppSettings
    {
        [DataMember] public string Theme { get; set; }
        [DataMember] public string AccentHex { get; set; }
        [DataMember] public string BackgroundImage { get; set; }
        [DataMember] public double BackgroundDim { get; set; }
        [DataMember] public bool MinimizeToTray { get; set; }
        [DataMember] public bool Animations { get; set; }
        [DataMember] public string SortMode { get; set; }
        [DataMember] public string ViewMode { get; set; }
        [DataMember] public double WindowWidth { get; set; }
        [DataMember] public double WindowHeight { get; set; }

        public AppSettings()
        {
            Theme = "dark";
            AccentHex = "#FF5B3A";
            BackgroundImage = "";
            BackgroundDim = 0.48;
            MinimizeToTray = false;
            Animations = true;
            SortMode = "recent";
            ViewMode = "grid";
            WindowWidth = 1360;
            WindowHeight = 860;
        }
    }

    [DataContract]
    public class AppData
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public DateTime UpdatedUtc { get; set; }
        [DataMember] public List<GameEntry> Games { get; set; }
        [DataMember] public AppSettings Settings { get; set; }

        public AppData()
        {
            Version = 1;
            Games = new List<GameEntry>();
            Settings = new AppSettings();
        }
    }

    [DataContract]
    public class ExportGame
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string ExePath { get; set; }
        [DataMember] public string Arguments { get; set; }
        [DataMember] public string WorkingDirectory { get; set; }
        [DataMember] public string Description { get; set; }
        [DataMember] public string Developer { get; set; }
        [DataMember] public string Genre { get; set; }
        [DataMember] public string ReleaseYear { get; set; }
        [DataMember] public bool Favorite { get; set; }
        [DataMember] public DateTime AddedUtc { get; set; }
        [DataMember] public DateTime? LastPlayedUtc { get; set; }
        [DataMember] public long TotalSeconds { get; set; }
        [DataMember] public int LaunchCount { get; set; }
        [DataMember] public List<PlaySession> Sessions { get; set; }
        [DataMember] public DateTime? ActiveSessionStartUtc { get; set; }
        [DataMember] public DateTime? ActiveSessionLastSeenUtc { get; set; }
        [DataMember] public string CoverData { get; set; }
        [DataMember] public string BackgroundData { get; set; }
        [DataMember] public string LogoData { get; set; }

        public ExportGame()
        {
            Sessions = new List<PlaySession>();
        }
    }

    [DataContract]
    public class ExportPackage
    {
        [DataMember] public string Format { get; set; }
        [DataMember] public int Version { get; set; }
        [DataMember] public string Kind { get; set; }
        [DataMember] public DateTime ExportedUtc { get; set; }
        [DataMember] public List<ExportGame> Games { get; set; }

        public ExportPackage()
        {
            Format = "cloudgame.library";
            Version = 1;
            Kind = "play";
            ExportedUtc = DateTime.UtcNow;
            Games = new List<ExportGame>();
        }
    }

    public class ImportResult
    {
        public int Added { get; set; }
        public int Merged { get; set; }
        public int Sessions { get; set; }
        public int Skipped { get; set; }
    }
}
