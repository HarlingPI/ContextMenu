// 本文件由 Codex 新增：视频包含查询的 pHash 序列匹配器

#region 由 Codex 添加
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace CustomTools.Tools
{
    internal sealed class VideoContainmentMatch
    {
        public double StartSeconds { get; }
        public double AverageDistance { get; }
        public double Confidence { get; }
        public int ComparedFrames { get; }

        public VideoContainmentMatch(
            double startSeconds,
            double averageDistance,
            double confidence,
            int comparedFrames)
        {
            StartSeconds = startSeconds;
            AverageDistance = averageDistance;
            Confidence = confidence;
            ComparedFrames = comparedFrames;
        }
    }

    internal static class VideoContainmentMatcher
    {
        private const int HashLength = VideoCheckCache.HashLength;
        private const int PrimaryAnchorThreshold = 48;
        private const int SecondaryAnchorThreshold = 56;
        private const int MaximumAverageDistance = 38;
        private const int MaximumFrameDistance = 72;
        private const double MinimumGoodFrameRatio = 0.65;
        private const int MaximumCandidates = 160;
        private const int MaximumVerificationFrames = 96;

        // 由 Codex 修改：全零哈希代表黑屏等低信息画面，不能作为包含匹配证据
        private const int MinimumComparedFrames = 6;

        public static VideoContainmentMatch? Find(
            byte[] queryHashes,
            byte[] targetHashes,
            double samplesPerSecond)
        {
            var queryCount = queryHashes.Length / HashLength;
            var targetCount = targetHashes.Length / HashLength;
            if (queryCount < 6 || targetCount < queryCount)
            {
                return null;
            }


            // 由 Codex 修改：只使用非全零有效帧，并移除哈希种类数量的硬门槛
            var informativeQueryCount = CountInformativeHashes(queryHashes, queryCount);
            if (informativeQueryCount < MinimumComparedFrames)
            {
                return null;
            }

            var anchorIndices = GetAnchorIndices(queryHashes, queryCount);
            if (anchorIndices.Length == 0)
            {
                return null;
            }

            var primaryAnchor = anchorIndices[anchorIndices.Length / 2];
            var candidates = new Dictionary<int, int>();

            for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
            {
                if (!IsInformativeHash(targetHashes, targetIndex))
                {
                    continue;
                }

                var distance = HashDistance(queryHashes, primaryAnchor, targetHashes, targetIndex);
                if (distance > PrimaryAnchorThreshold)
                {
                    continue;
                }

                var startIndex = targetIndex - primaryAnchor;
                if (startIndex < 0 || startIndex + queryCount > targetCount)
                {
                    continue;
                }

                if (!candidates.TryGetValue(startIndex, out var previous) || distance < previous)
                {
                    candidates[startIndex] = distance;
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var gatedCandidates = candidates.Keys
                .Select(startIndex => (StartIndex: startIndex, AnchorDistance: GetAnchorDistance(
                    queryHashes,
                    targetHashes,
                    anchorIndices,
                    startIndex)))
                .Where(candidate => candidate.AnchorDistance >= 0)
                .OrderBy(candidate => candidate.AnchorDistance)
                .Take(MaximumCandidates)
                .ToArray();

            VideoContainmentMatch? best = null;
            foreach (var candidate in gatedCandidates)
            {
                for (int offset = -2; offset <= 2; offset++)
                {
                    var startIndex = candidate.StartIndex + offset;
                    if (startIndex < 0 || startIndex + queryCount > targetCount)
                    {
                        continue;
                    }

                    var match = VerifyWindow(
                        queryHashes,
                        targetHashes,
                        startIndex,
                        queryCount,
                        samplesPerSecond);
                    if (match != null &&
                        (best == null || match.AverageDistance < best.AverageDistance))
                    {
                        best = match;
                    }
                }
            }

            return best;
        }

        // 由 Codex 修改：锚点优先选择非全零帧，避免短候选被黑屏锚点主导
        private static int[] GetAnchorIndices(byte[] hashes, int count)
        {
            var desiredIndices = new[]
            {
                count / 5,
                count / 2,
                count * 4 / 5
            };
            var anchors = new HashSet<int>();
            foreach (var desiredIndex in desiredIndices)
            {
                var anchorIndex = FindNearestInformativeHash(hashes, count, desiredIndex);
                if (anchorIndex >= 0)
                {
                    anchors.Add(anchorIndex);
                }
            }
            return anchors.OrderBy(index => index).ToArray();
        }

        private static int FindNearestInformativeHash(byte[] hashes, int count, int desiredIndex)
        {
            if (IsInformativeHash(hashes, desiredIndex))
            {
                return desiredIndex;
            }

            for (int offset = 1; offset < count; offset++)
            {
                var leftIndex = desiredIndex - offset;
                if (leftIndex >= 0 && IsInformativeHash(hashes, leftIndex))
                {
                    return leftIndex;
                }

                var rightIndex = desiredIndex + offset;
                if (rightIndex < count && IsInformativeHash(hashes, rightIndex))
                {
                    return rightIndex;
                }
            }
            return -1;
        }

        private static int GetAnchorDistance(
            byte[] queryHashes,
            byte[] targetHashes,
            int[] anchorIndices,
            int startIndex)
        {
            var matchedAnchors = 0;
            var totalDistance = 0;
            foreach (var queryIndex in anchorIndices)
            {
                var targetIndex = startIndex + queryIndex;
                if (!IsInformativeHash(targetHashes, targetIndex))
                {
                    continue;
                }

                var distance = HashDistance(
                    queryHashes,
                    queryIndex,
                    targetHashes,
                    targetIndex);
                totalDistance += distance;
                if (distance <= SecondaryAnchorThreshold)
                {
                    matchedAnchors++;
                }
            }

            return matchedAnchors >= Math.Min(2, anchorIndices.Length)
                ? totalDistance
                : -1;
        }

        private static VideoContainmentMatch? VerifyWindow(
            byte[] queryHashes,
            byte[] targetHashes,
            int startIndex,
            int queryCount,
            double samplesPerSecond)
        {
            var step = Math.Max(1, queryCount / MaximumVerificationFrames);
            long totalDistance = 0;
            var comparedFrames = 0;
            var goodFrames = 0;

            for (int queryIndex = 0; queryIndex < queryCount; queryIndex += step)
            {
                var targetIndex = startIndex + queryIndex;
                if (!IsInformativeHash(queryHashes, queryIndex) ||
                    !IsInformativeHash(targetHashes, targetIndex))
                {
                    continue;
                }

                var distance = HashDistance(
                    queryHashes,
                    queryIndex,
                    targetHashes,
                    targetIndex);
                totalDistance += distance;
                comparedFrames++;
                if (distance <= MaximumFrameDistance)
                {
                    goodFrames++;
                }
            }

            if (comparedFrames < MinimumComparedFrames)
            {
                return null;
            }

            var averageDistance = totalDistance / (double)comparedFrames;
            var goodRatio = goodFrames / (double)comparedFrames;
            if (averageDistance > MaximumAverageDistance ||
                goodRatio < MinimumGoodFrameRatio)
            {
                return null;
            }

            var confidence = Math.Clamp(1 - averageDistance / 256d, 0, 1);
            return new VideoContainmentMatch(
                startIndex / Math.Max(samplesPerSecond, 0.001),
                averageDistance,
                confidence,
                comparedFrames);
        }

        private static int HashDistance(byte[] left, int leftIndex, byte[] right, int rightIndex)
        {
            var leftOffset = leftIndex * HashLength;
            var rightOffset = rightIndex * HashLength;
            ulong difference =
                BinaryPrimitives.ReadUInt64LittleEndian(left.AsSpan(leftOffset, 8)) ^
                BinaryPrimitives.ReadUInt64LittleEndian(right.AsSpan(rightOffset, 8));
            var count = PopCount(difference);

            difference =
                BinaryPrimitives.ReadUInt64LittleEndian(left.AsSpan(leftOffset + 8, 8)) ^
                BinaryPrimitives.ReadUInt64LittleEndian(right.AsSpan(rightOffset + 8, 8));
            count += PopCount(difference);

            difference =
                BinaryPrimitives.ReadUInt64LittleEndian(left.AsSpan(leftOffset + 16, 8)) ^
                BinaryPrimitives.ReadUInt64LittleEndian(right.AsSpan(rightOffset + 16, 8));
            count += PopCount(difference);

            difference =
                BinaryPrimitives.ReadUInt64LittleEndian(left.AsSpan(leftOffset + 24, 8)) ^
                BinaryPrimitives.ReadUInt64LittleEndian(right.AsSpan(rightOffset + 24, 8));
            count += PopCount(difference);
            return count;
        }

        private static int CountInformativeHashes(byte[] hashes, int count)
        {
            var informativeCount = 0;
            for (int index = 0; index < count; index++)
            {
                if (IsInformativeHash(hashes, index))
                {
                    informativeCount++;
                }
            }
            return informativeCount;
        }

        internal static bool IsInformativeHash(byte[] hashes, int index)
        {
            var offset = index * HashLength;
            for (int byteIndex = 0; byteIndex < HashLength; byteIndex++)
            {
                if (hashes[offset + byteIndex] != 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static int PopCount(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) +
                    ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
        }

    }
}
#endregion
