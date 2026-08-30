// 本文件由 Codex 新增

#region 由 Codex 添加
using ConsoleKit;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomTools.Tools
{
    /// <summary>
    /// 视频查重工具：抽取视频帧生成 pHash，按汉明距离分组，再由用户在窗口中选择删除。
    /// </summary>
    [MenuItem("视频查重", 5, Catgray.File)]
    public class VideoCheck : ITool
    {
        private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm",
            ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".3gp", ".ogv", ".vob"
        };

        private const int FrameSize = 32 * 32;
        private const double CropRatio = 0.15;

        private readonly int frameCount;
        private readonly int threshold;
        private readonly double durationTolerance;
        private readonly int parallelism;
        private readonly string hwaccel;
        private readonly string ffmpegPath;
        private readonly string ffprobePath;
        private readonly object consoleLock = new object();
        private int hwaccelFallbackReported;

        public VideoCheck()
        {
            var config = ToolsConfig.LoadVideoCheck();
            frameCount = config.VideoFrameCount;
            threshold = config.VideoThreshold;
            durationTolerance = config.VideoDurationTolerance;
            parallelism = config.VideoParallelism;
            hwaccel = config.VideoHardwareAccel;

            var baseDir = AppContext.BaseDirectory;
            ffmpegPath = Path.Combine(baseDir, "FFmpeg", "ffmpeg.exe");
            ffprobePath = Path.Combine(baseDir, "FFmpeg", "ffprobe.exe");
        }

        public void Process(string path)
        {
            var searchTask = Task.Run(() =>
            {
                return FileUtils.SearchFiles(path, ".*", true)
                    .Where(file => VideoExtensions.Contains(Path.GetExtension(file)))
                    .ToArray();
            });
            Effects.ShowSpinner2Char("Searching", searchTask);

            var files = searchTask.Result
                .OrderBy(file => new FileInfo(file).Length)
                .ToArray();

            Console.WriteLine($"发现 {files.Length} 个视频文件");
            if (files.Length == 0)
            {
                MessageBox.Show("当前目录下未发现视频文件。", "视频查重", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AutoCloseConsole();
                return;
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(parallelism, 1, Environment.ProcessorCount)
            };

            // 阶段1a：仅获取时长
            var durations = new double[files.Length];
            int probed = 0;
            var total = files.Length;
            var phaseTimerDuration = Stopwatch.StartNew();
            Ansi.HideCursor();
            Console.Write($"时长获取进度:{Effects.ProgressBar(40, 0)}(0/{total}) {BuildProgressTime(0, total, phaseTimerDuration.Elapsed)}");
            Parallel.For(0, total, parallelOptions, i =>
            {
                try
                {
                    durations[i] = GetDuration(files[i]);
                }
                catch
                {
                    durations[i] = 0;
                }

                var current = Interlocked.Increment(ref probed);
                lock (consoleLock)
                {
                    Ansi.ClearCurtLine();
                    Console.Write($"时长获取进度:{Effects.ProgressBar(40, current / (float)total)}({current}/{total}) {BuildProgressTime(current, total, phaseTimerDuration.Elapsed)}");
                }
            });
            Ansi.ClearCurtLine();
            Console.Write($"时长获取进度:{Effects.ProgressBar(40, 1)}({total}/{total}) {BuildProgressTime(total, total, phaseTimerDuration.Elapsed)}");
            Ansi.ShowCursor();
            Console.WriteLine();

            // 阶段1b：仅计算内容 ID
            var ids = new byte[files.Length][];
            var caches = LoadVideoCaches(files);
            var dirtyDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int idDone = 0;
            var phaseTimerId = Stopwatch.StartNew();
            Ansi.HideCursor();
            Console.Write($"内容 ID 进度:{Effects.ProgressBar(40, 0)}(0/{total}) {BuildProgressTime(0, total, phaseTimerId.Elapsed)}");
            Parallel.For(0, total, parallelOptions, i =>
            {
                var directory = Path.GetFullPath(Path.GetDirectoryName(files[i]) ?? ".");
                var dirCache = caches.TryGetValue(directory, out var loaded) ? loaded : null;
                string signatureKey = string.Empty;
                try
                {
                    signatureKey = VideoCheckCache.ComputeSignatureKey(files[i], durations[i]);
                }
                catch
                {
                    // 忽略无法读取元数据的文件
                }

                if (dirCache != null &&
                    signatureKey.Length > 0 &&
                    dirCache.BySignature.TryGetValue(signatureKey, out var cachedList) &&
                    cachedList.Count == 1)
                {
                    ids[i] = cachedList[0].Id;
                }
                else
                {
                    try
                    {
                        ids[i] = VideoCheckCache.ComputeContentId(files[i]);
                    }
                    catch
                    {
                        ids[i] = Array.Empty<byte>();
                    }
                }

                var current = Interlocked.Increment(ref idDone);
                lock (consoleLock)
                {
                    Ansi.ClearCurtLine();
                    Console.Write($"内容 ID 进度:{Effects.ProgressBar(40, current / (float)total)}({current}/{total}) {BuildProgressTime(current, total, phaseTimerId.Elapsed)}");
                }
            });
            Ansi.ClearCurtLine();
            Console.Write($"内容 ID 进度:{Effects.ProgressBar(40, 1)}({total}/{total}) {BuildProgressTime(total, total, phaseTimerId.Elapsed)}");
            Ansi.ShowCursor();
            Console.WriteLine();

            // 阶段2：只有存在时长相近同伴的视频才需要计算 pHash
            var candidates = FilterDurationCandidates(durations);
            Console.WriteLine($"时长初筛后需要计算 pHash 的视频：{candidates.Length}/{total}");
            if (candidates.Length < 2)
            {
                UpdateCacheFiles(caches, ids, files, dirtyDirs);
                MessageBox.Show("没有时长相近的视频，无法比较。", "视频查重", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AutoCloseConsole();
                return;
            }

            // 阶段3：先做缓存检查，命中直接复用，未命中才计算
            var results = new VideoResult?[candidates.Length];
            var toCompute = new List<int>();
            for (int i = 0; i < candidates.Length; i++)
            {
                var index = candidates[i];
                var directory = Path.GetFullPath(Path.GetDirectoryName(files[index]) ?? ".");
                var cache = caches.TryGetValue(directory, out var dirCache) ? dirCache : null;
                var key = ids[index].Length > 0 ? VideoCheckCache.ToKey(ids[index]) : string.Empty;
                if (cache != null && key.Length > 0 && cache.ById.TryGetValue(key, out var entry) && entry.Hashes.Length > 0)
                {
                    results[i] = new VideoResult(files[index], durations[index], entry.Hashes);
                }
                else
                {
                    toCompute.Add(i);
                }
            }
            Console.WriteLine($"缓存检查后实际需要计算 pHash 的视频：{toCompute.Count}/{candidates.Length}");

            if (toCompute.Count > 0)
            {
                int computed = 0;
                var computeTotal = toCompute.Count;
                var phaseTimerPhash = Stopwatch.StartNew();
                Ansi.HideCursor();
                Console.Write($"pHash 进度:{Effects.ProgressBar(40, 0)}(0/{computeTotal}) {BuildProgressTime(0, computeTotal, phaseTimerPhash.Elapsed)}");

                Parallel.For(0, computeTotal, parallelOptions, i =>
                {
                    var candidateIndex = toCompute[i];
                    var index = candidates[candidateIndex];
                    try
                    {
                        var frameHashes = ComputePhashes(files[index]);
                        if (frameHashes.Count == 0)
                        {
                            throw new InvalidOperationException("未能提取到有效帧");
                        }
                        var hashes = frameHashes.ToArray();
                        results[candidateIndex] = new VideoResult(files[index], durations[index], hashes);

                        var directory = Path.GetFullPath(Path.GetDirectoryName(files[index]) ?? ".");
                        var cache = caches.TryGetValue(directory, out var dirCache) ? dirCache : null;
                        var key = ids[index].Length > 0 ? VideoCheckCache.ToKey(ids[index]) : string.Empty;
                        if (cache != null && key.Length > 0)
                        {
                            lock (consoleLock)
                            {
                                var info = new FileInfo(files[index]);
                                cache.Add(new CacheEntry(ids[index], info.Length, info.LastWriteTimeUtc.Ticks, durations[index], hashes));
                                dirtyDirs.Add(directory);
                                VideoCheckCache.Write(directory, cache.ById.Values);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (consoleLock)
                        {
                            Ansi.ClearCurtLine();
                            Console.WriteLine($"处理失败：{Path.GetFileName(files[index])} {ex.Message}");
                        }
                    }

                    var current = Interlocked.Increment(ref computed);
                    lock (consoleLock)
                    {
                        Ansi.ClearCurtLine();
                        Console.Write($"pHash 进度:{Effects.ProgressBar(40, current / (float)computeTotal)}({current}/{computeTotal}) {BuildProgressTime(current, computeTotal, phaseTimerPhash.Elapsed)}");
                    }
                });

                Ansi.ClearCurtLine();
                Console.Write($"pHash 进度:{Effects.ProgressBar(40, 1)}({computeTotal}/{computeTotal}) {BuildProgressTime(computeTotal, computeTotal, phaseTimerPhash.Elapsed)}");
                Ansi.ShowCursor();
                Console.WriteLine();
            }

            UpdateCacheFiles(caches, ids, files, dirtyDirs);

            var videos = results.Where(item => item != null).Cast<VideoResult>().ToArray();
            Console.WriteLine($"有效视频：{videos.Length}");
            if (videos.Length < 2)
            {
                MessageBox.Show("无法对少于两个视频进行比较。", "视频查重", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AutoCloseConsole();
                return;
            }

            var groups = MatchGroups(videos);
            ShowResults(groups);
            UpdateCacheFiles(caches, ids, files, dirtyDirs);
            AutoCloseConsole();
        }

        private static Dictionary<string, DirectoryCache> LoadVideoCaches(string[] files)
        {
            var caches = new Dictionary<string, DirectoryCache>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var directory = Path.GetFullPath(Path.GetDirectoryName(file) ?? ".");
                if (!caches.ContainsKey(directory))
                {
                    caches[directory] = VideoCheckCache.Load(directory);
                }
            }
            return caches;
        }

        private void UpdateCacheFiles(
            Dictionary<string, DirectoryCache> caches,
            byte[][] ids,
            string[] files,
            HashSet<string> dirtyDirs)
        {
            foreach (var directory in caches.Keys.ToArray())
            {
                var cache = caches[directory];
                var currentIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < files.Length; i++)
                {
                    var fileDir = Path.GetFullPath(Path.GetDirectoryName(files[i]) ?? ".");
                    if (string.Equals(fileDir, directory, StringComparison.OrdinalIgnoreCase) &&
                        ids[i].Length > 0 &&
                        File.Exists(files[i]))
                    {
                        currentIds.Add(VideoCheckCache.ToKey(ids[i]));
                    }
                }

                var removed = false;
                foreach (var idKey in cache.ById.Keys.ToArray())
                {
                    if (!currentIds.Contains(idKey))
                    {
                        var entry = cache.ById[idKey];
                        cache.Remove(idKey, VideoCheckCache.SignatureKey(entry.Length, entry.LastWriteTimeTicks, entry.Duration));
                        removed = true;
                    }
                }
                if (removed)
                {
                    dirtyDirs.Add(directory);
                }

                if (!dirtyDirs.Contains(directory))
                {
                    continue;
                }

                if (cache.ById.Count > 0)
                {
                    VideoCheckCache.Write(directory, cache.ById.Values);
                }
                else
                {
                    var path = VideoCheckCache.GetCachePath(directory);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }
        }

        private int[] FilterDurationCandidates(double[] durations)
        {
            var hasPeer = new bool[durations.Length];
            var order = Enumerable.Range(0, durations.Length)
                .Where(i => durations[i] > 0)
                .OrderBy(i => durations[i])
                .ToArray();

            for (int oi = 0; oi < order.Length; oi++)
            {
                var i = order[oi];
                for (int oj = oi + 1; oj < order.Length; oj++)
                {
                    var j = order[oj];
                    if (durations[j] - durations[i] > durationTolerance)
                    {
                        break;
                    }
                    if (IsDurationMatch(durations[i], durations[j]))
                    {
                        hasPeer[i] = true;
                        hasPeer[j] = true;
                    }
                }
            }

            return Enumerable.Range(0, durations.Length).Where(i => hasPeer[i]).ToArray();
        }

        private static void AutoCloseConsole()
        {
            // 给用户留出查看结果的时间，每秒刷新倒计时，10 秒后关闭控制台
            for (var remain = 10; remain > 0; remain--)
            {
                Console.Write(($"\r10 秒后自动关闭窗口...{remain} 秒").PadRight(40));
                Thread.Sleep(1000);
            }
            Console.Write(new string(' ', 40));
            Console.Write("\r");
            Console.Out.Flush();
            Environment.Exit(0);
        }

        private double GetDuration(string file)
        {
            var arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{file}\"";
            using var process = StartProcess(ffprobePath, arguments);
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(error.Trim());
            }

            return double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private List<byte[]> ComputePhashes(string file)
        {
            if (hwaccel.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractPhashes(file, "none");
            }

            try
            {
                return ExtractPhashes(file, hwaccel);
            }
            catch
            {
                // 硬件解码失败时回退到 CPU 解码
                var cpuHashes = ExtractPhashes(file, "none");
                ReportHardwareFallback();
                return cpuHashes;
            }
        }

        private List<byte[]> ExtractPhashes(string file, string accel)
        {
            var outputRate = frameCount + 1;
            var crop = $"crop=iw-2*round(iw*{CropRatio}):ih-2*round(ih*{CropRatio}):round(iw*{CropRatio}):round(ih*{CropRatio})";
            var filter = $"fps={outputRate},select='not(eq(mod(n,{outputRate}),0))',format=gray,{crop},scale=32:32:flags=bilinear";
            var accelArg = accel.Equals("none", StringComparison.OrdinalIgnoreCase) ? string.Empty : $"-hwaccel {accel} ";
            var arguments = $"-hide_banner -loglevel error -nostdin {accelArg}-i \"{file}\" -vf \"{filter}\" -an -fps_mode vfr -pix_fmt gray -f rawvideo -";

            using var process = StartProcess(ffmpegPath, arguments);
            var errorTask = process.StandardError.ReadToEndAsync();
            var stream = process.StandardOutput.BaseStream;
            var buffer = new byte[FrameSize];
            var context = new PhashContext();
            var hashes = new List<byte[]>();

            while (true)
            {
                var total = 0;
                var ended = false;
                while (total < FrameSize)
                {
                    var read = stream.Read(buffer, total, FrameSize - total);
                    if (read <= 0)
                    {
                        ended = true;
                        break;
                    }
                    total += read;
                }

                if (ended)
                {
                    if (total > 0)
                    {
                        throw new InvalidOperationException("帧数据不完整");
                    }
                    break;
                }

                hashes.Add(context.ComputeHash(buffer));
            }

            process.WaitForExit();
            var error = errorTask.Result;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(error.Trim());
            }

            return hashes;
        }

        private void ReportHardwareFallback()
        {
            // 只提示一次，避免并行场景刷屏
            if (Interlocked.CompareExchange(ref hwaccelFallbackReported, 1, 0) != 0)
            {
                return;
            }
            Console.WriteLine("硬件解码不可用，已自动回退 CPU 解码");
        }

        private List<VideoGroup> MatchGroups(VideoResult[] items)
        {
            var parent = new int[items.Length];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            var order = Enumerable.Range(0, items.Length).OrderBy(i => items[i].Duration).ToArray();
            for (int oi = 0; oi < order.Length; oi++)
            {
                var i = order[oi];
                var a = items[i];
                for (int oj = oi + 1; oj < order.Length; oj++)
                {
                    var j = order[oj];
                    var b = items[j];

                    // 按时长排序后，若当前视频时长已超出容差，后续视频只会更远
                    if (b.Duration - a.Duration > durationTolerance)
                    {
                        break;
                    }

                    if (!IsDurationMatch(a.Duration, b.Duration))
                    {
                        continue;
                    }

                    if (AverageHamming(a.Hashes, b.Hashes) <= threshold)
                    {
                        Union(parent, i, j);
                    }
                }
            }

            var groups = new Dictionary<int, List<VideoResult>>();
            for (int i = 0; i < items.Length; i++)
            {
                var root = Find(parent, i);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<VideoResult>();
                    groups.Add(root, list);
                }
                list.Add(items[i]);
            }

            return groups.Values
                .Where(group => group.Count > 1)
                .Select(group => new VideoGroup(group))
                .OrderByDescending(group => group.Items.Count)
                .ToList();
        }

        private bool IsDurationMatch(double a, double b)
        {
            return Math.Abs(a - b) <= durationTolerance;
        }

        private static double AverageHamming(byte[][] a, byte[][] b)
        {
            var count = Math.Min(a.Length, b.Length);
            if (count == 0)
            {
                return double.MaxValue;
            }

            long sum = 0;
            for (int f = 0; f < count; f++)
            {
                var ha = a[f];
                var hb = b[f];
                for (int i = 0; i < ha.Length; i++)
                {
                    sum += PopCount((uint)(ha[i] ^ hb[i]));
                }
            }
            return sum / (double)count;
        }

        private static int PopCount(uint value)
        {
            var count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }
            return count;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Union(int[] parent, int a, int b)
        {
            var rootA = Find(parent, a);
            var rootB = Find(parent, b);
            if (rootA != rootB)
            {
                parent[rootA] = rootB;
            }
        }

        private void ShowResults(List<VideoGroup> groups)
        {
            if (groups.Count == 0)
            {
                MessageBox.Show("未发现可能相同的视频。", "视频查重", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using var window = new VideoCheckWindow(groups);
            window.ShowDialog();

            if (window.DeletedCount > 0)
            {
                Console.WriteLine($"已删除 {window.DeletedCount} 个视频，共 {FormatSize(window.DeletedBytes)}");
                if (window.FailedCount > 0)
                {
                    Console.WriteLine($"删除失败 {window.FailedCount} 个");
                }
            }
            else
            {
                Console.WriteLine("未删除任何视频");
            }
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

        private static string BuildProgressTime(int current, int total, TimeSpan elapsed)
        {
            var remaining = current > 0
                ? TimeSpan.FromSeconds(elapsed.TotalSeconds / current * (total - current))
                : TimeSpan.Zero;
            return $"已耗时:{FormatTime(elapsed)} 剩余:{FormatTime(remaining)}";
        }

        private static string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
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

    }

    internal sealed class VideoResult
    {
        public string FilePath { get; }
        public double Duration { get; }
        public byte[][] Hashes { get; }

        public VideoResult(string filePath, double duration, byte[][] hashes)
        {
            FilePath = filePath;
            Duration = duration;
            Hashes = hashes;
        }
    }

    internal sealed class VideoGroup
    {
        public List<VideoResult> Items { get; }

        public VideoGroup(List<VideoResult> items)
        {
            Items = items;
        }
    }

    /// <summary>
    /// 视频查重工具对应的配置段，通过扁平配置文件读取。
    /// </summary>
    public sealed partial class ToolsConfig
    {
        public int VideoFrameCount = 2;
        public int VideoThreshold = 30;
        public double VideoDurationTolerance = 10;
        public int VideoParallelism = 4;
        public string VideoHardwareAccel = "none";

        public static ToolsConfig LoadVideoCheck()
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
                if (key == "video.fps" && int.TryParse(value, out var fps) && fps > 0)
                {
                    config.VideoFrameCount = fps;
                }
                else if (key == "video.threshold" && int.TryParse(value, out var th) && th >= 0)
                {
                    config.VideoThreshold = th;
                }
                else if (key == "video.parallelism" && int.TryParse(value, out var parallelism) && parallelism > 0)
                {
                    config.VideoParallelism = parallelism;
                }
                else if (key == "video.hwaccel" && value.Length > 0)
                {
                    config.VideoHardwareAccel = value;
                }
                else if (key == "video.durationTolerance" &&
                    double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var tolerance) && tolerance >= 0)
                {
                    config.VideoDurationTolerance = tolerance;
                }
            }

            return config;
        }
    }
}
#endregion
