// 本文件由 Codex 新增：使用低分辨率帧序列校准视频包含位置并过滤误报

#region 由 Codex 添加
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace CustomTools.Tools
{
    internal sealed class VideoContainmentVisualResult
    {
        public VideoContainmentVisualStatus Status { get; }
        public double StartSeconds { get; }
        public double CandidateOffsetSeconds { get; }
        public double Score { get; }

        public VideoContainmentVisualResult(
            VideoContainmentVisualStatus status,
            double startSeconds,
            double candidateOffsetSeconds,
            double score)
        {
            Status = status;
            StartSeconds = startSeconds;
            CandidateOffsetSeconds = candidateOffsetSeconds;
            Score = score;
        }
    }

    internal enum VideoContainmentVisualStatus
    {
        Matched,
        Rejected,
        Skipped
    }

    internal sealed class VideoContainmentVisualVerifier
    {
        private const int FrameWidth = 16;
        private const int FrameHeight = 16;
        private const int FrameSize = FrameWidth * FrameHeight;
        private const double CropRatio = 0.15;
        private const int SearchRadiusSeconds = 60;
        private const int MaximumCandidateOffsetSeconds = 60;
        private const double MinimumOverlapRatio = 0.8;
        private const double MaximumAcceptedScore = 0.35;
        private const double ScoreMargin = 0.2;

        private readonly string ffmpegPath;
        private readonly ConcurrentDictionary<string, Lazy<VideoFrameSet?>> sourceFrameCache =
            new(StringComparer.OrdinalIgnoreCase);

        public VideoContainmentVisualVerifier(string? ffmpegPath = null)
        {
            this.ffmpegPath = ffmpegPath ??
                Path.Combine(AppContext.BaseDirectory, "FFmpeg", "ffmpeg.exe");
        }

        public VideoContainmentVisualResult Verify(
            string sourceFile,
            string candidateFile,
            double sourceDuration,
            double candidateDuration,
            double approximateStartSeconds)
        {
            try
            {
                var sourceFrames = sourceFrameCache.GetOrAdd(
                    sourceFile,
                    file => new Lazy<VideoFrameSet?>(() => ExtractFrames(file, 0, 0)))
                    .Value;
                var candidateFrames = ExtractFrames(candidateFile, 0, candidateDuration);
                if (sourceFrames == null ||
                    candidateFrames == null ||
                    sourceFrames.Count == 0 ||
                    candidateFrames.Count == 0)
                {
                    return new VideoContainmentVisualResult(
                        VideoContainmentVisualStatus.Skipped,
                        approximateStartSeconds,
                        0,
                        1);
                }

                var requiredOverlap = Math.Max(
                    3,
                    (int)Math.Ceiling(candidateFrames.Count * MinimumOverlapRatio));
                var searchStart = Math.Clamp(
                    (int)Math.Floor(approximateStartSeconds) - SearchRadiusSeconds,
                    0,
                    Math.Max(0, sourceFrames.Count - 1));
                var searchEnd = Math.Min(
                    sourceFrames.Count - requiredOverlap,
                    (int)Math.Ceiling(approximateStartSeconds) + SearchRadiusSeconds);
                if (searchEnd < searchStart)
                {
                    return new VideoContainmentVisualResult(
                        VideoContainmentVisualStatus.Rejected,
                        approximateStartSeconds,
                        0,
                        1);
                }

                var candidateOffsetLimit = Math.Min(
                    MaximumCandidateOffsetSeconds,
                    Math.Max(0, candidateFrames.Count - requiredOverlap));
                var best = FindBest(
                    sourceFrames,
                    candidateFrames,
                    searchStart,
                    searchEnd,
                    candidateOffsetLimit,
                    requiredOverlap);
                if (best == null || best.Value.Score > MaximumAcceptedScore)
                {
                    return new VideoContainmentVisualResult(
                        VideoContainmentVisualStatus.Rejected,
                        approximateStartSeconds,
                        0,
                        best?.Score ?? 1);
                }

                var earliest = FindEarliest(
                    sourceFrames,
                    candidateFrames,
                    searchStart,
                    searchEnd,
                    candidateOffsetLimit,
                    requiredOverlap,
                    Math.Min(MaximumAcceptedScore, best.Value.Score + ScoreMargin)) ?? best.Value;
                return new VideoContainmentVisualResult(
                    VideoContainmentVisualStatus.Matched,
                    earliest.SourceOffset,
                    earliest.CandidateOffset,
                    earliest.Score);
            }
            catch
            {
                return new VideoContainmentVisualResult(
                    VideoContainmentVisualStatus.Skipped,
                    approximateStartSeconds,
                    0,
                    1);
            }
        }

        private static (int SourceOffset, int CandidateOffset, double Score, int Overlap)? FindBest(
            VideoFrameSet source,
            VideoFrameSet candidate,
            int searchStart,
            int searchEnd,
            int candidateOffsetLimit,
            int requiredOverlap)
        {
            (int SourceOffset, int CandidateOffset, double Score, int Overlap)? best = null;
            for (int candidateOffset = 0; candidateOffset <= candidateOffsetLimit; candidateOffset++)
            {
                for (int sourceOffset = searchStart; sourceOffset <= searchEnd; sourceOffset++)
                {
                    var overlap = Math.Min(
                        candidate.Count - candidateOffset,
                        source.Count - sourceOffset);
                    if (overlap < requiredOverlap)
                    {
                        continue;
                    }

                    var score = SequenceDistance(
                        candidate,
                        candidateOffset,
                        source,
                        sourceOffset,
                        overlap,
                        48);
                    if (best == null || score < best.Value.Score)
                    {
                        best = (sourceOffset, candidateOffset, score, overlap);
                    }
                }
            }
            return best;
        }

        private static (int SourceOffset, int CandidateOffset, double Score, int Overlap)? FindEarliest(
            VideoFrameSet source,
            VideoFrameSet candidate,
            int searchStart,
            int searchEnd,
            int candidateOffsetLimit,
            int requiredOverlap,
            double maximumScore)
        {
            for (int sourceOffset = searchStart; sourceOffset <= searchEnd; sourceOffset++)
            {
                for (int candidateOffset = 0; candidateOffset <= candidateOffsetLimit; candidateOffset++)
                {
                    var overlap = Math.Min(
                        candidate.Count - candidateOffset,
                        source.Count - sourceOffset);
                    if (overlap < requiredOverlap)
                    {
                        continue;
                    }

                    var probeScore = SequenceDistance(
                        candidate,
                        candidateOffset,
                        source,
                        sourceOffset,
                        Math.Min(10, overlap),
                        10);
                    if (probeScore > MaximumAcceptedScore)
                    {
                        continue;
                    }

                    var score = SequenceDistance(
                        candidate,
                        candidateOffset,
                        source,
                        sourceOffset,
                        overlap,
                        48);
                    if (score <= maximumScore)
                    {
                        return (sourceOffset, candidateOffset, score, overlap);
                    }
                }
            }
            return null;
        }

        private VideoFrameSet? ExtractFrames(string file, double startSeconds, double duration)
        {
            var crop = $"crop=iw-2*round(iw*{CropRatio}):ih-2*round(ih*{CropRatio}):round(iw*{CropRatio}):round(ih*{CropRatio})";
            var filter = $"fps=1,format=gray,{crop},scale={FrameWidth}:{FrameHeight}:flags=bilinear";
            var durationArgument = duration > 0
                ? $"-t {duration.ToString(CultureInfo.InvariantCulture)} "
                : string.Empty;
            var startArgument = startSeconds > 0
                ? $"-ss {startSeconds.ToString(CultureInfo.InvariantCulture)} "
                : string.Empty;
            using var process = Process.Start(new ProcessStartInfo(ffmpegPath)
            {
                Arguments = $"-hide_banner -loglevel error -nostdin {startArgument}{durationArgument}-i \"{file}\" -vf \"{filter}\" -an -fps_mode vfr -pix_fmt gray -f rawvideo -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("无法启动 ffmpeg");

            using var output = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(output);
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(error);
            }

            var data = output.ToArray();
            return new VideoFrameSet(data, data.Length / FrameSize);
        }

        private static double SequenceDistance(
            VideoFrameSet candidate,
            int candidateOffset,
            VideoFrameSet source,
            int sourceOffset,
            int overlap,
            int maximumSamples)
        {
            var step = Math.Max(1, overlap / maximumSamples);
            double total = 0;
            var count = 0;
            for (int index = 0; index < overlap; index += step)
            {
                total += FrameDistance(
                    candidate,
                    candidateOffset + index,
                    source,
                    sourceOffset + index);
                count++;
            }
            return count == 0 ? 1 : total / count;
        }

        private static double FrameDistance(
            VideoFrameSet left,
            int leftFrame,
            VideoFrameSet right,
            int rightFrame)
        {
            var leftOffset = leftFrame * FrameSize;
            var rightOffset = rightFrame * FrameSize;
            double sumLeft = 0;
            double sumRight = 0;
            double sumLeftSquare = 0;
            double sumRightSquare = 0;
            double sumProduct = 0;
            for (int index = 0; index < FrameSize; index++)
            {
                double leftValue = left.Data[leftOffset + index];
                double rightValue = right.Data[rightOffset + index];
                sumLeft += leftValue;
                sumRight += rightValue;
                sumLeftSquare += leftValue * leftValue;
                sumRightSquare += rightValue * rightValue;
                sumProduct += leftValue * rightValue;
            }

            var covariance = sumProduct - sumLeft * sumRight / FrameSize;
            var leftVariance = sumLeftSquare - sumLeft * sumLeft / FrameSize;
            var rightVariance = sumRightSquare - sumRight * sumRight / FrameSize;
            if (leftVariance <= 0.001 || rightVariance <= 0.001)
            {
                return 1;
            }

            var correlation = covariance / Math.Sqrt(leftVariance * rightVariance);
            return 1 - Math.Clamp(correlation, -1, 1);
        }

        private sealed class VideoFrameSet
        {
            public byte[] Data { get; }
            public int Count { get; }

            public VideoFrameSet(byte[] data, int count)
            {
                Data = data;
                Count = count;
            }
        }
    }
}
#endregion
