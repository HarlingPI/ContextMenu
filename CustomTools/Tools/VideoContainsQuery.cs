// 本文件由 Codex 新增：基于 pHash 缓存的全局视频包含查询工具

#region 由 Codex 添加
using ConsoleKit;
using PIToolKit.Public.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CustomTools.Tools
{
    [MenuItem("视频包含查询", 6, Catgray.File)]
    public sealed class VideoContainsQuery : ITool
    {
        private const int MaximumCandidatesPerSource = 256;

        private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm",
            ".m4v", ".mpg", ".mpeg", ".ts", ".mts", ".3gp", ".ogv", ".vob"
        };

        private readonly int expectedSamplesPerSecond;
        private readonly object consoleLock = new object();

        public VideoContainsQuery()
        {
            expectedSamplesPerSecond = Math.Max(1, ToolsConfig.LoadVideoCheck().VideoFrameCount);
        }

        public void Process(string path)
        {
            var searchRoot = ResolveSearchRoot(Path.GetFullPath(path));
            if (searchRoot == null)
            {
                MessageBox.Show("无法确定要扫描的目录。", "视频包含查询", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var searchTask = Task.Run(() => FileUtils.SearchFiles(searchRoot, ".*", true)
                .Where(file => VideoExtensions.Contains(Path.GetExtension(file)))
                .ToArray());
            Effects.ShowSpinner2Char("Searching", searchTask);
            var files = searchTask.Result;
            if (files.Length < 2)
            {
                MessageBox.Show("当前目录下至少需要两个视频才能进行包含查询。", "视频包含查询", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Console.WriteLine($"搜索目录：{searchRoot}");
            Console.WriteLine($"视频文件：{files.Length} 个");

            var inventory = LoadInventory(files);
            if (inventory.Count < 2)
            {
                MessageBox.Show("没有足够的有效 pHash 缓存，无法进行包含查询。", "视频包含查询", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var groups = FindContainmentGroups(inventory);
            if (groups.Count == 0)
            {
                MessageBox.Show("未发现源视频包含其他完整视频。", "视频包含查询", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using var window = new VideoContainsWindow(groups);
            window.ShowDialog();

            // 由 Codex 修改：结果窗口关闭后合并本次删除产生的增量缓存
            var cacheCompactFailedCount = 0;
            if (window.DeletedCount > 0 && window.DeletedCacheDirectories.Count > 0)
            {
                Console.WriteLine($"开始合并 {window.DeletedCacheDirectories.Count} 个目录的 pHash 缓存...");
                foreach (var directory in window.DeletedCacheDirectories)
                {
                    try
                    {
                        VideoCheckCache.Compact(directory);
                    }
                    catch (Exception ex)
                    {
                        cacheCompactFailedCount++;
                        Console.WriteLine($"pHash 缓存合并失败：{directory}");
                        Console.WriteLine(ex.Message);
                    }
                }

                if (cacheCompactFailedCount == 0)
                {
                    Console.WriteLine("pHash 缓存合并完成");
                }
                else
                {
                    Console.WriteLine(
                        $"pHash 缓存合并失败 {cacheCompactFailedCount} 个目录，增量日志已保留。");
                }
            }

            if (window.DeletedCount > 0)
            {
                Console.WriteLine(
                    $"已删除 {window.DeletedCount} 个文件，总大小 {FormatSize(window.DeletedBytes)}");
            }
            else
            {
                Console.WriteLine("未删除任何文件");
            }

            if (window.FailedCount > 0)
            {
                Console.WriteLine($"删除失败 {window.FailedCount} 个文件");
            }

            AutoCloseConsole();
        }

        private List<CachedVideo> LoadInventory(string[] files)
        {
            var inventory = new ConcurrentBag<CachedVideo>();
            var groups = files
                .GroupBy(file => Path.GetFullPath(Path.GetDirectoryName(file) ?? "."))
                .ToArray();
            var completed = 0;
            var skippedWithoutCache = 0;
            var skippedRateMismatch = 0;
            var stopwatch = Stopwatch.StartNew();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8)
            };

            Ansi.HideCursor();
            Console.Write(
                $"缓存索引进度:{Effects.ProgressBar(40, 0)}(0/{files.Length}) 已耗时:{FormatTime(stopwatch.Elapsed)} 剩余:{FormatTime(TimeSpan.Zero)}");
            foreach (var group in groups)
            {
                DirectoryCache cache;
                try
                {
                    cache = VideoCheckCache.Load(group.Key);
                }
                catch
                {
                    var failedCount = group.Count();
                    Interlocked.Add(ref skippedWithoutCache, failedCount);
                    var currentFailed = Interlocked.Add(ref completed, failedCount);
                    WriteInventoryProgress(currentFailed, files.Length, inventory.Count, stopwatch.Elapsed);
                    continue;
                }

                var entriesByIdentity = BuildIdentityIndex(cache);
                Parallel.ForEach(group, parallelOptions, file =>
                {
                    try
                    {
                        if (!entriesByIdentity.TryGetValue(GetFileIdentity(file), out var entries))
                        {
                            Interlocked.Increment(ref skippedWithoutCache);
                            return;
                        }

                        var entry = entries
                            .Where(candidate =>
                                candidate.Hashes.Length >= VideoCheckCache.HashLength * 6 &&
                                candidate.Duration > 0)
                            .OrderByDescending(candidate => candidate.Hashes.Length)
                            .FirstOrDefault();
                        if (entry == null)
                        {
                            Interlocked.Increment(ref skippedWithoutCache);
                            return;
                        }

                        var samplesPerSecond =
                            entry.Hashes.Length / VideoCheckCache.HashLength / entry.Duration;
                        if (Math.Abs(samplesPerSecond - expectedSamplesPerSecond) > 0.15)
                        {
                            Interlocked.Increment(ref skippedRateMismatch);
                            return;
                        }

                        inventory.Add(new CachedVideo(file, entry.Duration, entry.Hashes));
                    }
                    catch
                    {
                        // 单个缓存记录异常时继续扫描其他视频
                    }
                    finally
                    {
                        var current = Interlocked.Increment(ref completed);
                        if (current == files.Length || current % 64 == 0)
                        {
                            WriteInventoryProgress(current, files.Length, inventory.Count, stopwatch.Elapsed);
                        }
                    }
                });
            }

            Ansi.ClearCurtLine();
            Console.Write(
                $"缓存索引进度:{Effects.ProgressBar(40, 1)}({files.Length}/{files.Length}) 已耗时:{FormatTime(stopwatch.Elapsed)} 剩余:{FormatTime(TimeSpan.Zero)}");
            Ansi.ShowCursor();
            Console.WriteLine();
            Console.WriteLine(
                $"缓存索引完成：可用 {inventory.Count} 个，无缓存 {skippedWithoutCache} 个，采样率不匹配 {skippedRateMismatch} 个");

            return inventory
                .OrderByDescending(video => video.Duration)
                .ThenBy(video => video.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<VideoContainmentGroup> FindContainmentGroups(List<CachedVideo> inventory)
        {
            var anchorIndex = BuildAnchorIndex(inventory);
            var containedVideoIds = new HashSet<int>();
            var groups = new List<VideoContainmentGroup>();
            var timer = Stopwatch.StartNew();
            var totalMatchWork = inventory.Sum(
                video => (long)(video.Hashes.Length / VideoCheckCache.HashLength));
            long completedMatchWork = 0;

            Ansi.HideCursor();
            for (int sourceId = 0; sourceId < inventory.Count; sourceId++)
            {
                var source = inventory[sourceId];
                if (containedVideoIds.Contains(sourceId))
                {
                    completedMatchWork += source.Hashes.Length / VideoCheckCache.HashLength;
                    if (sourceId == inventory.Count - 1 || sourceId % 32 == 0)
                    {
                        WriteMatchProgress(
                            sourceId + 1,
                            inventory.Count,
                            containedVideoIds.Count,
                            completedMatchWork,
                            totalMatchWork,
                            timer.Elapsed);
                    }
                    continue;
                }

                var hitCounts = FindCandidateHits(
                    sourceId,
                    inventory,
                    anchorIndex,
                    containedVideoIds);
                var candidates = hitCounts
                    .OrderByDescending(pair => pair.Value)
                    .Take(MaximumCandidatesPerSource)
                    .Select(pair => new CandidateHit(pair.Key, inventory[pair.Key]))
                    .ToArray();

                var found = new ConcurrentBag<VerifiedContainmentCandidate>();
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8)
                };
                Parallel.ForEach(candidates, parallelOptions, candidate =>
                {
                    if (containedVideoIds.Contains(candidate.VideoId) ||
                        candidate.Video.Duration > source.Duration - 1)
                    {
                        return;
                    }

                    var match = VideoContainmentMatcher.Find(
                        candidate.Video.Hashes,
                        source.Hashes,
                        source.SamplesPerSecond);
                    if (match == null)
                    {
                        return;
                    }

                    found.Add(new VerifiedContainmentCandidate(
                        candidate.VideoId,
                        new VideoContainsResult(
                            candidate.Video.FilePath,
                            match.StartSeconds,
                            match.Confidence,
                            match.AverageDistance,
                            candidate.Video.Duration)));
                });

                if (!found.IsEmpty)
                {
                    var sourceVisualVerifier = new VideoContainmentVisualVerifier();
                    var items = found
                        .OrderByDescending(item => item.Result.Confidence)
                        .ThenBy(item => item.Result.FilePath, StringComparer.OrdinalIgnoreCase)
                        .Select(item =>
                        {
                            // 由 Codex 修改：使用低分辨率时间轴复核位置并过滤误报
                            var visual = sourceVisualVerifier.Verify(
                                source.FilePath,
                                item.Result.FilePath,
                                source.Duration,
                                item.Result.Duration,
                                item.Result.MatchStartSeconds);
                            return visual.Status == VideoContainmentVisualStatus.Rejected
                                ? null
                                : new VerifiedContainmentCandidate(
                                    item.VideoId,
                                    new VideoContainsResult(
                                        item.Result.FilePath,
                                        visual.Status == VideoContainmentVisualStatus.Matched
                                            ? visual.StartSeconds
                                            : item.Result.MatchStartSeconds,
                                        item.Result.Confidence,
                                        item.Result.AverageDistance,
                                        item.Result.Duration));
                        })
                        .Where(item => item != null)
                        .Cast<VerifiedContainmentCandidate>()
                        .ToList();
                    if (items.Count > 0)
                    {
                        groups.Add(new VideoContainmentGroup(
                            source.FilePath,
                            source.Duration,
                            items.Select(item => item.Result).ToList()));

                        foreach (var item in items)
                        {
                            containedVideoIds.Add(item.VideoId);
                        }
                    }
                }

                completedMatchWork += source.Hashes.Length / VideoCheckCache.HashLength;
                if (sourceId == inventory.Count - 1 || sourceId % 16 == 0)
                {
                    WriteMatchProgress(
                        sourceId + 1,
                        inventory.Count,
                        containedVideoIds.Count,
                        completedMatchWork,
                        totalMatchWork,
                        timer.Elapsed);
                }
            }

            Ansi.ClearCurtLine();
            Ansi.ShowCursor();
            Console.WriteLine(
                $"包含查询完成：容器 {groups.Count} 个，已确认包含 {containedVideoIds.Count} 个视频，耗时 {FormatTime(timer.Elapsed)}");
            return groups;
        }

        private static Dictionary<ulong, List<AnchorPosting>> BuildAnchorIndex(
            IReadOnlyList<CachedVideo> inventory)
        {
            var index = new Dictionary<ulong, List<AnchorPosting>>();
            for (int videoId = 0; videoId < inventory.Count; videoId++)
            {
                var video = inventory[videoId];
                foreach (var anchorIndex in video.AnchorIndices)
                {
                    for (int band = 0; band < 4; band++)
                    {
                        // 由 Codex 修改：全零分块不进入候选索引，避免黑屏产生大量假候选
                        if (!TryGetChunkKey(video.Hashes, anchorIndex, band, out var key))
                        {
                            continue;
                        }

                        if (!index.TryGetValue(key, out var postings))
                        {
                            postings = new List<AnchorPosting>();
                            index[key] = postings;
                        }
                        postings.Add(new AnchorPosting(videoId, anchorIndex));
                    }
                }
            }

            return index;
        }

        private static Dictionary<int, int> FindCandidateHits(
            int sourceId,
            IReadOnlyList<CachedVideo> inventory,
            IReadOnlyDictionary<ulong, List<AnchorPosting>> anchorIndex,
            HashSet<int> containedVideoIds)
        {
            var source = inventory[sourceId];
            var sourceHashCount = source.Hashes.Length / VideoCheckCache.HashLength;
            var hits = new Dictionary<int, int>();

            for (int sourceIndex = 0; sourceIndex < sourceHashCount; sourceIndex++)
            {
                if (!VideoContainmentMatcher.IsInformativeHash(source.Hashes, sourceIndex))
                {
                    continue;
                }

                for (int band = 0; band < 4; band++)
                {
                    if (!TryGetChunkKey(source.Hashes, sourceIndex, band, out var key))
                    {
                        continue;
                    }

                    if (!anchorIndex.TryGetValue(key, out var postings))
                    {
                        continue;
                    }

                    foreach (var posting in postings)
                    {
                        if (posting.VideoId == sourceId ||
                            containedVideoIds.Contains(posting.VideoId) ||
                            inventory[posting.VideoId].Duration > source.Duration - 1)
                        {
                            continue;
                        }

                        hits.TryGetValue(posting.VideoId, out var count);
                        hits[posting.VideoId] = count + 1;
                    }
                }
            }

            return hits;
        }

        private static bool TryGetChunkKey(
            byte[] hashes,
            int hashIndex,
            int band,
            out ulong key)
        {
            var offset = hashIndex * VideoCheckCache.HashLength + band * sizeof(uint);
            var chunk = BinaryPrimitives.ReadUInt32LittleEndian(hashes.AsSpan(offset, sizeof(uint)));
            if (chunk == 0)
            {
                key = 0;
                return false;
            }

            key = ((ulong)band << 32) | chunk;
            return true;
        }

        private static Dictionary<(long Length, long Ticks), List<CacheEntry>> BuildIdentityIndex(
            DirectoryCache cache)
        {
            return cache.ById.Values
                .GroupBy(entry => (entry.Length, entry.LastWriteTimeTicks))
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        private static (long Length, long Ticks) GetFileIdentity(string file)
        {
            var info = new FileInfo(file);
            return (info.Length, info.LastWriteTimeUtc.Ticks);
        }

        private static string? ResolveSearchRoot(string path)
        {
            if (Directory.Exists(path))
            {
                return path;
            }

            return File.Exists(path) ? Path.GetDirectoryName(path) : null;
        }

        private void WriteInventoryProgress(
            int current,
            int total,
            int available,
            TimeSpan elapsed)
        {
            lock (consoleLock)
            {
                Ansi.ClearCurtLine();
                Console.Write(
                    $"缓存索引进度:{Effects.ProgressBar(40, current / (float)total)}({current}/{total}) 可用:{available} 已耗时:{FormatTime(elapsed)} 剩余:{FormatTime(EstimateRemaining(current, total, elapsed))}");
            }
        }

        private void WriteMatchProgress(
            int current,
            int total,
            int contained,
            long completedWork,
            long totalWork,
            TimeSpan elapsed)
        {
            lock (consoleLock)
            {
                Ansi.ClearCurtLine();
                var remaining = EstimateRemaining(completedWork, totalWork, elapsed);
                Console.Write(
                    $"包含匹配进度:{Effects.ProgressBar(40, current / (float)total)}({current}/{total}) 已包含:{contained} 已耗时:{FormatTime(elapsed)} 剩余:{FormatTime(remaining)}");
            }
        }

        private static TimeSpan EstimateRemaining(long completed, long total, TimeSpan elapsed)
        {
            if (completed <= 0 || total <= completed || elapsed <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            var remainingSeconds = elapsed.TotalSeconds / completed * (total - completed);
            return remainingSeconds >= TimeSpan.MaxValue.TotalSeconds
                ? TimeSpan.MaxValue
                : TimeSpan.FromSeconds(remainingSeconds);
        }

        private static string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        #region 由 Codex 添加：结果窗口关闭后的控制台统计

        private static string FormatSize(long bytes)
        {
            const long kb = 1024;
            const long mb = 1024 * 1024;
            const long gb = 1024 * 1024 * 1024;
            if (bytes >= gb)
            {
                return $"{bytes / (double)gb:F2} GB";
            }
            if (bytes >= mb)
            {
                return $"{bytes / (double)mb:F2} MB";
            }
            return $"{Math.Max(1, bytes / (double)kb):F0} KB";
        }

        private static void AutoCloseConsole()
        {
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

        #endregion

        private sealed class CachedVideo
        {
            public string FilePath { get; }
            public double Duration { get; }
            public byte[] Hashes { get; }
            public int[] AnchorIndices { get; }
            public double SamplesPerSecond =>
                Hashes.Length / VideoCheckCache.HashLength / Math.Max(Duration, 0.001);

            public CachedVideo(string filePath, double duration, byte[] hashes)
            {
                FilePath = filePath;
                Duration = duration;
                Hashes = hashes;
                AnchorIndices = CreateAnchorIndices(
                    hashes,
                    hashes.Length / VideoCheckCache.HashLength);
            }

            private static int[] CreateAnchorIndices(byte[] hashes, int hashCount)
            {
                if (hashCount == 0)
                {
                    return Array.Empty<int>();
                }

                var informativeIndices = new List<int>();
                for (int index = 0; index < hashCount; index++)
                {
                    if (VideoContainmentMatcher.IsInformativeHash(hashes, index))
                    {
                        informativeIndices.Add(index);
                    }
                }
                if (informativeIndices.Count == 0)
                {
                    return Array.Empty<int>();
                }

                // 由 Codex 修改：锚点选择最接近目标位置的非全零哈希
                const int anchorCount = 8;
                var anchors = new HashSet<int>();
                for (int index = 0; index < anchorCount; index++)
                {
                    var desiredIndex = (int)((index + 0.5) * hashCount / anchorCount);
                    var insertionIndex = informativeIndices.BinarySearch(desiredIndex);
                    if (insertionIndex < 0)
                    {
                        insertionIndex = ~insertionIndex;
                    }

                    var leftIndex = Math.Clamp(insertionIndex - 1, 0, informativeIndices.Count - 1);
                    var rightIndex = Math.Clamp(insertionIndex, 0, informativeIndices.Count - 1);
                    var leftDistance = Math.Abs(informativeIndices[leftIndex] - desiredIndex);
                    var rightDistance = Math.Abs(informativeIndices[rightIndex] - desiredIndex);
                    anchors.Add(informativeIndices[leftDistance <= rightDistance ? leftIndex : rightIndex]);
                }
                return anchors.OrderBy(index => index).ToArray();
            }
        }

        private readonly struct AnchorPosting
        {
            public int VideoId { get; }
            public int AnchorIndex { get; }

            public AnchorPosting(int videoId, int anchorIndex)
            {
                VideoId = videoId;
                AnchorIndex = anchorIndex;
            }
        }

        private sealed class CandidateHit
        {
            public int VideoId { get; }
            public CachedVideo Video { get; }

            public CandidateHit(int videoId, CachedVideo video)
            {
                VideoId = videoId;
                Video = video;
            }
        }

        private sealed class VerifiedContainmentCandidate
        {
            public int VideoId { get; }
            public VideoContainsResult Result { get; }

            public VerifiedContainmentCandidate(int videoId, VideoContainsResult result)
            {
                VideoId = videoId;
                Result = result;
            }
        }
    }
}
#endregion
