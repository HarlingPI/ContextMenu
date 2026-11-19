// 本文件由 GPT-5 新增：图片查重工具，复用 pHash 计算并按汉明距离分组

#region 由 GPT-5 添加
using ConsoleKit;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomTools.Tools
{
    /// <summary>
    /// 图片查重工具：将图片缩放到 32x32 灰度图后计算 pHash，并按汉明距离分组。
    /// </summary>
    [MenuItem("图片查重", 6, Catgray.File)]
    public class ImageCheck : ITool
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".bmp", ".dib", ".gif",
            ".tif", ".tiff", ".webp", ".avif", ".heic", ".heif", ".ico", ".tga"
        };

        private const int ImageSize = 32;
        private const int GrayPixelCount = ImageSize * ImageSize;

        private readonly int threshold;
        private readonly int parallelism;
        private readonly string ffmpegPath;
        private readonly object consoleLock = new object();

        public ImageCheck()
        {
            var config = ToolsConfig.LoadImageCheck();
            threshold = Math.Clamp(config.ImageThreshold, 0, PhashContext.HashLength * 8);
            parallelism = Math.Clamp(config.ImageParallelism, 1, Environment.ProcessorCount);
            ffmpegPath = Path.Combine(AppContext.BaseDirectory, "FFmpeg", "ffmpeg.exe");
        }

        public void Process(string path)
        {
            var searchTask = Task.Run(() =>
            {
                return FileUtils.SearchFiles(path, ".*", true)
                    .Where(file => ImageExtensions.Contains(Path.GetExtension(file)))
                    .ToArray();
            });
            Effects.ShowSpinner2Char("Searching", searchTask);

            var files = searchTask.Result
                .OrderBy(file => new FileInfo(file).Length)
                .ToArray();
            Console.WriteLine($"发现 {files.Length} 个图片文件");

            if (files.Length == 0)
            {
                MessageBox.Show(
                    "当前目录下未发现图片文件。",
                    "图片查重",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                AutoCloseConsole();
                return;
            }

            // 由 GPT-5 修改：图片 pHash 使用签名、内容 ID、pHash 三级缓存，与文件名无关

            var fileInfos = files.Select(file => new FileInfo(file)).ToArray();
            var fileSizes = fileInfos.Select(info => info.Length).ToArray();
            var fileWriteTicks = fileInfos.Select(info => info.LastWriteTimeUtc.Ticks).ToArray();
            var caches = LoadImageCaches(files);
            var dirtyDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var contentIds = new byte[]?[files.Length];
            var results = new ImageResult?[files.Length];
            var toCompute = new List<int>();
            var signatureHits = 0;
            var contentIdHits = 0;
            var failedCount = 0;
            for (var i = 0; i < files.Length; i++)
            {
                var directory = Path.GetFullPath(Path.GetDirectoryName(files[i]) ?? ".");
                var cache = caches[directory];
                if (cache.TryGetUniqueBySignature(fileInfos[i], out var entry))
                {
                    contentIds[i] = entry.Id;
                    results[i] = new ImageResult(files[i], entry.Hash);
                    signatureHits++;
                }
                else
                {
                    try
                    {
                        var contentId = ImageCheckCache.ComputeContentId(files[i]);
                        contentIds[i] = contentId;
                        if (cache.TryGetById(contentId, out entry))
                        {
                            results[i] = new ImageResult(files[i], entry.Hash);
                            var updatedEntry = new ImageCacheEntry(
                                contentId,
                                fileSizes[i],
                                fileWriteTicks[i],
                                entry.Hash);
                            try
                            {
                                ImageCheckCache.Append(directory, updatedEntry);
                                cache.Add(updatedEntry);
                                dirtyDirs.Add(directory);
                            }
                            catch (Exception cacheException)
                            {
                                Console.WriteLine($"图片 pHash 增量缓存写入失败：{Path.GetFileName(files[i])} {cacheException.Message}");
                            }
                            contentIdHits++;
                        }
                        else
                        {
                            toCompute.Add(i);
                        }
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        Console.WriteLine($"读取图片内容 ID 失败：{Path.GetFileName(files[i])} {ex.Message}");
                    }
                }
            }

            Console.WriteLine(
                $"图片 pHash 三级缓存：签名命中 {signatureHits}，内容 ID 命中 {contentIdHits}，需要计算 {toCompute.Count}");

            var totalWork = toCompute.Sum(index => GetHashWork(fileSizes[index]));
            var completed = 0;
            var estimator = new ProgressEstimator(totalWork);
            var timer = Stopwatch.StartNew();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelism
            };

            if (toCompute.Count > 0)
            {
                Ansi.HideCursor();
                Console.Write($"图片 pHash 进度:{Effects.ProgressBar(40, 0)}(0/{toCompute.Count}) {estimator.Build(timer.Elapsed)}");
                try
                {
                    Parallel.For(0, toCompute.Count, parallelOptions, i =>
                    {
                        var index = toCompute[i];
                        try
                        {
                            var hash = ComputeHash(files[index]);
                            results[index] = new ImageResult(files[index], hash);

                            var directory = Path.GetFullPath(Path.GetDirectoryName(files[index]) ?? ".");
                            var contentId = contentIds[index];
                            if (contentId == null)
                            {
                                throw new InvalidOperationException("图片内容 ID 尚未生成");
                            }
                            var entry = new ImageCacheEntry(
                                contentId,
                                fileSizes[index],
                                fileWriteTicks[index],
                                hash);
                            try
                            {
                                lock (caches[directory])
                                {
                                    ImageCheckCache.Append(directory, entry);
                                    caches[directory].Add(entry);
                                    dirtyDirs.Add(directory);
                                }
                            }
                            catch (Exception cacheException)
                            {
                                lock (consoleLock)
                                {
                                    Ansi.ClearCurtLine();
                                    Console.WriteLine($"图片 pHash 增量缓存写入失败：{Path.GetFileName(files[index])} {cacheException.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failedCount);
                            lock (consoleLock)
                            {
                                Ansi.ClearCurtLine();
                                Console.WriteLine($"处理失败：{Path.GetFileName(files[index])} {ex.Message}");
                            }
                        }

                        var current = Interlocked.Increment(ref completed);
                        lock (consoleLock)
                        {
                            estimator.Complete(GetHashWork(fileSizes[index]));
                            Ansi.ClearCurtLine();
                            Console.Write($"图片 pHash 进度:{Effects.ProgressBar(40, current / (float)toCompute.Count)}({current}/{toCompute.Count}) {estimator.Build(timer.Elapsed)}");
                        }
                    });
                }
                finally
                {
                    Ansi.ClearCurtLine();
                    Console.Write($"图片 pHash 进度:{Effects.ProgressBar(40, 1)}({toCompute.Count}/{toCompute.Count}) {estimator.Build(timer.Elapsed)}");
                    Ansi.ShowCursor();
                    Console.WriteLine();
                }
            }

            UpdateImageCaches(caches, files, contentIds, dirtyDirs);

            var images = results.Where(item => item != null).Cast<ImageResult>().ToArray();
            Console.WriteLine($"有效图片：{images.Length}，失败：{failedCount}");
            if (images.Length < 2)
            {
                MessageBox.Show(
                    "无法对少于两个有效图片进行比较。",
                    "图片查重",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                AutoCloseConsole();
                return;
            }

            var groups = MatchGroups(images);
            ShowResults(groups);
            UpdateImageCaches(caches, files, contentIds, dirtyDirs);
            AutoCloseConsole();
        }

        // 由 GPT-5 修改：加载增量日志并在结束后合并图片目录级 pHash 缓存

        private static Dictionary<string, ImageDirectoryCache> LoadImageCaches(string[] files)
        {
            var caches = new Dictionary<string, ImageDirectoryCache>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var directory = Path.GetFullPath(Path.GetDirectoryName(file) ?? ".");
                if (!caches.ContainsKey(directory))
                {
                    caches[directory] = ImageCheckCache.Load(directory);
                }
            }
            return caches;
        }

        private static void UpdateImageCaches(
            Dictionary<string, ImageDirectoryCache> caches,
            string[] files,
            byte[]?[] contentIds,
            HashSet<string> dirtyDirs)
        {
            var activeIdsByDirectory =
                new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < files.Length; i++)
            {
                if (contentIds[i] == null || !File.Exists(files[i]))
                {
                    continue;
                }

                var directory = Path.GetFullPath(Path.GetDirectoryName(files[i]) ?? ".");
                if (!activeIdsByDirectory.TryGetValue(directory, out var activeIds))
                {
                    activeIds = new HashSet<string>(StringComparer.Ordinal);
                    activeIdsByDirectory[directory] = activeIds;
                }
                activeIds.Add(ImageCheckCache.ToKey(contentIds[i]!));
            }

            foreach (var pair in caches)
            {
                var directory = pair.Key;
                var cache = pair.Value;
                var activeIds = activeIdsByDirectory.TryGetValue(directory, out var ids)
                    ? ids
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var staleEntries = cache.FindStaleEntries(activeIds);
                foreach (var staleEntry in staleEntries)
                {
                    try
                    {
                        ImageCheckCache.AppendDeletion(directory, staleEntry.Id);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"图片 pHash 删除增量写入失败：{directory} {ex.Message}");
                    }
                    cache.RemoveById(ImageCheckCache.ToKey(staleEntry.Id));
                    dirtyDirs.Add(directory);
                }

                if (!dirtyDirs.Contains(directory) && !ImageCheckCache.HasDelta(directory))
                {
                    continue;
                }

                try
                {
                    if (cache.Count > 0 && Directory.Exists(directory))
                    {
                        ImageCheckCache.Write(directory, cache.Entries);
                    }
                    else
                    {
                        ImageCheckCache.Delete(directory);
                    }
                    dirtyDirs.Remove(directory);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"图片 pHash 缓存写入失败：{directory} {ex.Message}");
                }
            }
        }

        private byte[] ComputeHash(string file)
        {
            byte[] gray;
            try
            {
                gray = ReadGrayscaleWithDrawing(file);
            }
            catch
            {
                gray = ReadGrayscaleWithFfmpeg(file);
            }

            var hash = new byte[PhashContext.HashLength];
            var context = new PhashContext();
            context.ComputeHash(gray, hash);
            return hash;
        }

        private static byte[] ReadGrayscaleWithDrawing(string file)
        {
            using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var source = Image.FromStream(stream, false, true);
            ApplyExifOrientation(source);

            using var bitmap = new Bitmap(ImageSize, ImageSize, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(
                    source,
                    new Rectangle(0, 0, ImageSize, ImageSize),
                    0,
                    0,
                    source.Width,
                    source.Height,
                    GraphicsUnit.Pixel);
            }

            var rectangle = new Rectangle(0, 0, ImageSize, ImageSize);
            var bitmapData = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                var pixels = new byte[bitmapData.Stride * ImageSize];
                Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
                var gray = new byte[GrayPixelCount];
                for (var y = 0; y < ImageSize; y++)
                {
                    var rowOffset = y * bitmapData.Stride;
                    for (var x = 0; x < ImageSize; x++)
                    {
                        var offset = rowOffset + x * 3;
                        var blue = pixels[offset];
                        var green = pixels[offset + 1];
                        var red = pixels[offset + 2];
                        gray[y * ImageSize + x] = (byte)((red * 77 + green * 150 + blue * 29) >> 8);
                    }
                }
                return gray;
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        private byte[] ReadGrayscaleWithFfmpeg(string file)
        {
            if (!File.Exists(ffmpegPath))
            {
                throw new FileNotFoundException("未找到 FFmpeg，无法解码该图片", ffmpegPath);
            }

            var arguments = $"-hide_banner -loglevel error -nostdin -i \"{file}\" -frames:v 1 -vf \"scale=32:32:flags=bilinear\" -an -pix_fmt gray -f rawvideo -";
            using var process = StartProcess(ffmpegPath, arguments);
            var errorTask = process.StandardError.ReadToEndAsync();
            var gray = new byte[GrayPixelCount];
            var total = 0;
            while (total < gray.Length)
            {
                var read = process.StandardOutput.BaseStream.Read(gray, total, gray.Length - total);
                if (read <= 0)
                {
                    break;
                }
                total += read;
            }

            process.WaitForExit();
            var error = errorTask.Result;
            if (process.ExitCode != 0 || total != gray.Length)
            {
                throw new InvalidDataException(string.IsNullOrWhiteSpace(error) ? "图片解码失败" : error.Trim());
            }
            return gray;
        }

        private static void ApplyExifOrientation(Image image)
        {
            try
            {
                const int orientationId = 0x0112;
                if (!image.PropertyIdList.Contains(orientationId))
                {
                    return;
                }

                var property = image.GetPropertyItem(orientationId);
                if (property?.Value is not { Length: > 0 } propertyValue)
                {
                    return;
                }

                var value = propertyValue[0];
                switch (value)
                {
                    case 2:
                        image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                        break;
                    case 3:
                        image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                        break;
                    case 4:
                        image.RotateFlip(RotateFlipType.Rotate180FlipX);
                        break;
                    case 5:
                        image.RotateFlip(RotateFlipType.Rotate90FlipX);
                        break;
                    case 6:
                        image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                        break;
                    case 7:
                        image.RotateFlip(RotateFlipType.Rotate270FlipX);
                        break;
                    case 8:
                        image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                        break;
                }
            }
            catch
            {
                // 部分图片包含损坏的 EXIF 信息，忽略方向标记继续计算
            }
        }

        private List<ImageGroup> MatchGroups(ImageResult[] items)
        {
            var parent = new int[items.Length];
            for (var i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            var searchTree = new PhashSearchTree();
            var matches = new List<int>();
            for (var i = 0; i < items.Length; i++)
            {
                matches.Clear();
                searchTree.Find(items[i].Hash, threshold, matches);
                foreach (var match in matches)
                {
                    Union(parent, i, match);
                }
                searchTree.Add(items[i], i);
            }

            var groups = new Dictionary<int, List<ImageResult>>();
            for (var i = 0; i < items.Length; i++)
            {
                var root = Find(parent, i);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<ImageResult>();
                    groups.Add(root, list);
                }
                list.Add(items[i]);
            }

            return groups.Values
                .Where(group => group.Count > 1)
                .Select(group => new ImageGroup(group))
                .OrderByDescending(group => group.Items.Count)
                .ToList();
        }

        private void ShowResults(List<ImageGroup> groups)
        {
            if (groups.Count == 0)
            {
                MessageBox.Show(
                    "未发现可能相同的图片。",
                    "图片查重",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var window = new ImageCheckWindow(groups, threshold);
            window.ShowDialog();

            if (window.DeletedCount > 0)
            {
                Console.WriteLine($"已删除 {window.DeletedCount} 个图片，共 {FormatSize(window.DeletedBytes)}");
                if (window.FailedCount > 0)
                {
                    Console.WriteLine($"删除失败 {window.FailedCount} 个");
                }
            }
            else
            {
                Console.WriteLine("未删除任何图片");
            }
        }

        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }
            return index;
        }

        private static void Union(int[] parent, int left, int right)
        {
            var leftRoot = Find(parent, left);
            var rightRoot = Find(parent, right);
            if (leftRoot != rightRoot)
            {
                parent[leftRoot] = rightRoot;
            }
        }

        private static long GetHashWork(long fileSize)
        {
            const long fixedWork = 2L * 1024 * 1024;
            return Math.Max(1, fileSize + fixedWork);
        }

        private static Process StartProcess(string fileName, string arguments)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            return System.Diagnostics.Process.Start(startInfo) ?? throw new InvalidOperationException($"无法启动进程：{fileName}");
        }

        private static string FormatSize(long bytes)
        {
            const long kb = 1024;
            const long mb = 1024 * 1024;
            const long gb = 1024 * 1024 * 1024;
            if (bytes >= gb) return $"{bytes / (double)gb:F2}G";
            if (bytes >= mb) return $"{bytes / (double)mb:F2}M";
            return $"{Math.Max(1, bytes / (double)kb):F0}KB";
        }

        private static void AutoCloseConsole()
        {
            for (var remain = 10; remain > 0; remain--)
            {
                Console.Write($"\r10 秒后自动关闭窗口...{remain} 秒".PadRight(40));
                Thread.Sleep(1000);
            }
            Console.Write(new string(' ', 40));
            Console.Write("\r");
            Console.Out.Flush();
            Environment.Exit(0);
        }

        private sealed class ProgressEstimator
        {
            private readonly long totalWork;
            private long completedWork;

            public ProgressEstimator(long totalWork)
            {
                this.totalWork = Math.Max(0, totalWork);
            }

            public void Complete(long work)
            {
                completedWork = Math.Min(totalWork, completedWork + Math.Max(0, work));
            }

            public string Build(TimeSpan elapsed)
            {
                var remainingWork = Math.Max(0, totalWork - completedWork);
                var remaining = completedWork > 0 && remainingWork > 0
                    ? TimeSpan.FromSeconds(elapsed.TotalSeconds / completedWork * remainingWork)
                    : TimeSpan.Zero;
                return $"已耗时:{FormatTime(elapsed)} 剩余:{FormatTime(remaining)}";
            }

            private static string FormatTime(TimeSpan time)
            {
                return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
            }
        }

        private sealed class PhashSearchTree
        {
            private Node? root;

            public void Add(ImageResult item, int index)
            {
                var newNode = new Node(item, index);
                if (root == null)
                {
                    root = newNode;
                    return;
                }

                var current = root;
                while (true)
                {
                    var distance = PhashContext.Distance(item.Hash, current.Item.Hash);
                    if (current.Children.TryGetValue(distance, out var child))
                    {
                        current = child;
                        continue;
                    }

                    current.Children.Add(distance, newNode);
                    return;
                }
            }

            public void Find(byte[] hash, int radius, List<int> matches)
            {
                Search(root, hash, radius, matches);
            }

            private static void Search(Node? node, byte[] hash, int radius, List<int> matches)
            {
                if (node == null)
                {
                    return;
                }

                var distance = PhashContext.Distance(hash, node.Item.Hash);
                if (distance <= radius)
                {
                    matches.Add(node.Index);
                }

                var lower = Math.Max(0, distance - radius);
                var upper = Math.Min(PhashContext.HashLength * 8, distance + radius);
                foreach (var child in node.Children)
                {
                    if (child.Key >= lower && child.Key <= upper)
                    {
                        Search(child.Value, hash, radius, matches);
                    }
                }
            }

            private sealed class Node
            {
                public ImageResult Item { get; }
                public int Index { get; }
                public Dictionary<int, Node> Children { get; } = new Dictionary<int, Node>();

                public Node(ImageResult item, int index)
                {
                    Item = item;
                    Index = index;
                }
            }
        }
    }

    internal sealed class ImageResult
    {
        public string FilePath { get; }
        public byte[] Hash { get; }

        public ImageResult(string filePath, byte[] hash)
        {
            FilePath = filePath;
            Hash = hash;
        }
    }

    internal sealed class ImageGroup
    {
        public List<ImageResult> Items { get; }

        public ImageGroup(IEnumerable<ImageResult> items)
        {
            Items = items.ToList();
        }
    }

    /// <summary>
    /// 图片查重工具对应的配置段，通过扁平配置文件读取。
    /// </summary>
    public sealed partial class ToolsConfig
    {
        public int ImageThreshold = 20;
        public int ImageParallelism = 4;

        public static ToolsConfig LoadImageCheck()
        {
            var config = new ToolsConfig();
            var lines = FileUtils.ReadAllLines(ConfigPath);
            if (lines == null)
            {
                return config;
            }

            foreach (var line in lines)
            {
                var index = line.IndexOf(" = ", StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                var key = line[..index].Trim();
                var value = line[(index + 3)..].Trim();
                if (key == "image.threshold" &&
                    int.TryParse(value, out var threshold) &&
                    threshold >= 0)
                {
                    config.ImageThreshold = threshold;
                }
                else if (key == "image.parallelism" &&
                    int.TryParse(value, out var parallelism) &&
                    parallelism > 0)
                {
                    config.ImageParallelism = parallelism;
                }
            }

            return config;
        }
    }
}
#endregion
