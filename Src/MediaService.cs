using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ludens
{
    public enum MediaKind
    {
        Cover = 0,
        Background = 1,
        Logo = 2
    }

    public static class MediaService
    {
        private sealed class CacheItem
        {
            public DateTime Stamp;
            public BitmapSource Image;
        }

        private static readonly Dictionary<string, CacheItem> Cache =
            new Dictionary<string, CacheItem>(StringComparer.OrdinalIgnoreCase);

        public static int Version { get; private set; }

        public static void EnsureArtwork(GameEntry game)
        {
            if (game == null || string.IsNullOrEmpty(game.Id)) return;
            string folder = AppPaths.GameMediaFolder(game.Id);
            Directory.CreateDirectory(folder);

            if (string.IsNullOrEmpty(game.CoverImage) || !File.Exists(AppPaths.Resolve(game.CoverImage)))
            {
                string path = Path.Combine(folder, "cover.png");
                try
                {
                    CoverArt.GenerateCover(game, path);
                    game.CoverImage = AppPaths.ToStoredPath(path);
                    game.CoverGenerated = true;
                }
                catch (Exception ex) { AppStore.Log(ex); }
            }

            if (!string.IsNullOrEmpty(game.LogoImage) && !File.Exists(AppPaths.Resolve(game.LogoImage)))
            {
                game.LogoImage = "";
            }
            Version++;
        }

        public static string RegenerateCover(GameEntry game)
        {
            if (game == null || string.IsNullOrEmpty(game.Id)) return "";
            string folder = AppPaths.GameMediaFolder(game.Id);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "cover-" + DateTime.Now.Ticks + ".png");
            CoverArt.GenerateCover(game, path);
            game.CoverImage = AppPaths.ToStoredPath(path);
            game.CoverGenerated = true;
            Version++;
            return path;
        }

        public static string ImportGameImage(GameEntry game, MediaKind kind, string sourcePath)
        {
            if (game == null || string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("找不到图片文件。", sourcePath);
            string folder = AppPaths.GameMediaFolder(game.Id);
            Directory.CreateDirectory(folder);

            string extension = kind == MediaKind.Logo ? ".png" : ".jpg";
            string fileName = kind == MediaKind.Cover ? "cover-custom" : (kind == MediaKind.Background ? "background-custom" : "logo-custom");
            string destination = Path.Combine(folder, fileName + extension);

            if (kind == MediaKind.Logo)
            {
                SaveResized(sourcePath, destination, 640, 320, true, 92);
            }
            else if (kind == MediaKind.Cover)
            {
                SaveResized(sourcePath, destination, 900, 1200, false, 90);
            }
            else
            {
                SaveResized(sourcePath, destination, 2560, 1440, false, 86);
            }

            string old = kind == MediaKind.Cover ? game.CoverImage : (kind == MediaKind.Background ? game.BackgroundImage : game.LogoImage);
            string stored = AppPaths.ToStoredPath(destination);
            if (kind == MediaKind.Cover)
            {
                game.CoverImage = stored;
                game.CoverGenerated = false;
            }
            else if (kind == MediaKind.Background)
            {
                game.BackgroundImage = stored;
                game.BackgroundGenerated = false;
            }
            else
            {
                game.LogoImage = stored;
            }

            TryDeleteOldMedia(old, destination);
            Version++;
            return stored;
        }

        public static string ImportAppBackground(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("找不到图片文件。", sourcePath);
            string folder = AppPaths.AppMediaFolder;
            Directory.CreateDirectory(folder);
            // A fresh name avoids WPF's URI image cache and any lingering file handles.
            string name = "app-background-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".jpg";
            string destination = Path.Combine(folder, name);
            SaveResized(sourcePath, destination, 2560, 1440, false, 88);
            Version++;
            return AppPaths.ToStoredPath(destination);
        }

        public static void DeleteAppBackground(string storedPath)
        {
            if (string.IsNullOrEmpty(storedPath)) return;
            try
            {
                string path = AppPaths.Resolve(storedPath);
                string folder = Path.GetFullPath(AppPaths.AppMediaFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(path);
                if (!full.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return;
                Cache.Remove(full);
                if (File.Exists(full)) File.Delete(full);
            }
            catch (Exception ex) { AppStore.Log(ex); }
        }

        public static BitmapSource Load(string storedPath)
        {
            if (string.IsNullOrEmpty(storedPath)) return null;
            string path = AppPaths.Resolve(storedPath);
            if (!File.Exists(path)) return null;
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            CacheItem item;
            if (Cache.TryGetValue(path, out item) && item.Stamp == stamp) return item.Image;
            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                if (Cache.Count > 80) Cache.Clear();
                Cache[path] = new CacheItem { Stamp = stamp, Image = image };
                return image;
            }
            catch (Exception ex)
            {
                AppStore.Log(ex);
                return null;
            }
        }

        public static void Invalidate(string storedPath)
        {
            if (string.IsNullOrEmpty(storedPath)) return;
            Cache.Remove(AppPaths.Resolve(storedPath));
            Version++;
        }

        public static void ClearCache()
        {
            Cache.Clear();
            Version++;
        }

        private static void SaveResized(string sourcePath, string destination, int maxWidth, int maxHeight, bool png, int quality)
        {
            BitmapSource source;
            using (FileStream fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(fs,
                    BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                    BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count == 0) throw new InvalidDataException("图片没有可用帧。");
                source = decoder.Frames[0];
            }
            if (source.PixelWidth < 2 || source.PixelHeight < 2)
                throw new InvalidDataException("图片尺寸过小。");

            BitmapSource target = HalveToFit(source, maxWidth, maxHeight);
            double scale = Math.Min(1.0, Math.Min((double)maxWidth / target.PixelWidth, (double)maxHeight / target.PixelHeight));
            if (scale < 0.999)
            {
                TransformedBitmap resized = new TransformedBitmap(target, new ScaleTransform(scale, scale));
                resized.Freeze();
                target = resized;
            }

            BitmapSource encodeSource = target;
            if (target.Format != PixelFormats.Bgra32 && target.Format != PixelFormats.Bgr32)
            {
                FormatConvertedBitmap converted = new FormatConvertedBitmap(target, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                encodeSource = converted;
            }

            BitmapEncoder encoder;
            if (png)
            {
                encoder = new PngBitmapEncoder();
            }
            else
            {
                JpegBitmapEncoder jpeg = new JpegBitmapEncoder();
                jpeg.QualityLevel = quality;
                encoder = jpeg;
            }
            encoder.Frames.Add(BitmapFrame.Create(encodeSource));
            string temp = destination + ".tmp";
            using (FileStream fs = new FileStream(temp, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }
            if (File.Exists(destination)) File.Delete(destination);
            File.Move(temp, destination);
        }

        /// <summary>
        /// Progressive halving avoids the aliasing/noise produced when a very large
        /// photo is shrunk to card size in one bilinear step.
        /// </summary>
        private static BitmapSource HalveToFit(BitmapSource source, int maxWidth, int maxHeight)
        {
            BitmapSource current = source;
            while (current.PixelWidth > maxWidth * 2 || current.PixelHeight > maxHeight * 2)
            {
                TransformedBitmap half = new TransformedBitmap(current, new ScaleTransform(0.5, 0.5));
                half.Freeze();
                current = half;
            }
            return current;
        }

        private static void TryDeleteOldMedia(string storedPath, string newPath)
        {
            if (string.IsNullOrEmpty(storedPath)) return;
            try
            {
                string old = AppPaths.Resolve(storedPath);
                if (string.Equals(old, newPath, StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(old)) File.Delete(old);
                Cache.Remove(old);
            }
            catch (Exception ex) { AppStore.Log(ex); }
        }

        public static BitmapSource LoadAppBackground()
        {
            AppSettings settings = AppStore.Instance.Settings;
            return Load(settings.BackgroundImage);
        }
    }
}
