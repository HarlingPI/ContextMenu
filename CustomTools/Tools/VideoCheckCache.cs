// 本文件由 Codex 新增

#region 由 Codex 添加
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CustomTools.Tools
{
    /// <summary>
    /// 视频查重缓存：以内容标识为键，按目录存储 pHash。
    /// </summary>
    internal sealed class CacheEntry
    {
        public byte[] Id { get; }
        public long Length { get; }
        public long LastWriteTimeTicks { get; }
        public double Duration { get; }

        // 由 Codex 修改：pHashes 改为连续存储，每个哈希固定占用 HashLength 字节

        public byte[] Hashes { get; }

        public CacheEntry(byte[] id, long length, long lastWriteTimeTicks, double duration, byte[] hashes)
        {
            Id = id;
            Length = length;
            LastWriteTimeTicks = lastWriteTimeTicks;
            Duration = duration;
            Hashes = hashes;
        }
    }

    internal sealed class DirectoryCache
    {
        public Dictionary<string, CacheEntry> ById { get; } = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        public Dictionary<string, List<CacheEntry>> BySignature { get; } = new Dictionary<string, List<CacheEntry>>(StringComparer.Ordinal);

        public void Add(CacheEntry entry)
        {
            var idKey = VideoCheckCache.ToKey(entry.Id);
            var signatureKey = VideoCheckCache.SignatureKey(entry.Length, entry.LastWriteTimeTicks, entry.Duration);

            if (ById.TryGetValue(idKey, out var previous))
            {
                RemoveFromSignature(idKey, VideoCheckCache.SignatureKey(
                    previous.Length,
                    previous.LastWriteTimeTicks,
                    previous.Duration));
            }

            ById[idKey] = entry;
            if (!BySignature.TryGetValue(signatureKey, out var list))
            {
                list = new List<CacheEntry>();
                BySignature[signatureKey] = list;
            }
            list.Add(entry);
        }

        public void Remove(string idKey, string signatureKey)
        {
            ById.Remove(idKey);
            RemoveFromSignature(idKey, signatureKey);
        }

        private void RemoveFromSignature(string idKey, string signatureKey)
        {
            if (BySignature.TryGetValue(signatureKey, out var list))
            {
                list.RemoveAll(entry => VideoCheckCache.ToKey(entry.Id) == idKey);
                if (list.Count == 0)
                {
                    BySignature.Remove(signatureKey);
                }
            }
        }
    }

    internal static class VideoCheckCache
    {
        private const string CacheFileName = ".phash.cache";
        private const string DeltaFileName = ".phash.cache.delta";
        private const int IdLength = 16;
        private const int DeltaVersion = 1;
        private const int DeltaHeaderSize = 8;
        private const int DeltaChecksumSize = 32;
        private const int DeltaPayloadFixedSize = IdLength + 8 + 8 + 8 + 4;

        // 由 Codex 修改：公开哈希长度，供抽帧与比较逻辑共用

        public const int HashLength = 32;

        private const int HeadSize = 1024 * 1024;
        private const int TailSize = 1024 * 1024;
        private const int SampleSize = 64 * 1024;
        private const long SampleInterval = 8L * 1024 * 1024;
        private static readonly byte[] Magic = { (byte)'V', (byte)'C', (byte)'H', (byte)'K' };
        private static readonly byte[] DeltaMagic = { (byte)'V', (byte)'C', (byte)'D', (byte)'J' };

        public static string GetCachePath(string directory)
        {
            return Path.Combine(directory, CacheFileName);
        }

        public static string GetDeltaPath(string directory)
        {
            return Path.Combine(directory, DeltaFileName);
        }

        public static string ToKey(byte[] id)
        {
            return Convert.ToHexString(id);
        }

        public static string SignatureKey(long length, long lastWriteTimeTicks, double duration)
        {
            return $"{length}:{lastWriteTimeTicks}:{duration:R}";
        }

        public static string ComputeSignatureKey(string file, double duration)
        {
            var info = new FileInfo(file);
            return SignatureKey(info.Length, info.LastWriteTimeUtc.Ticks, duration);
        }

        public static byte[] ComputeContentId(string file)
        {
            using var stream = File.OpenRead(file);
            var size = stream.Length;
            using var combined = new MemoryStream();
            var sizeBytes = BitConverter.GetBytes(size);
            combined.Write(sizeBytes, 0, sizeBytes.Length);

            if (size <= HeadSize + TailSize)
            {
                var full = new byte[(int)size];
                stream.Position = 0;
                stream.ReadExactly(full);
                combined.Write(full, 0, full.Length);
            }
            else
            {
                var head = new byte[HeadSize];
                stream.Position = 0;
                stream.ReadExactly(head);
                combined.Write(head, 0, head.Length);

                var tail = new byte[TailSize];
                stream.Position = size - TailSize;
                stream.ReadExactly(tail);
                combined.Write(tail, 0, tail.Length);

                long offset = HeadSize;
                long tailStart = size - TailSize;
                while (offset + SampleSize < tailStart)
                {
                    var sample = new byte[SampleSize];
                    stream.Position = offset;
                    stream.ReadExactly(sample);
                    combined.Write(sample, 0, sample.Length);
                    offset += SampleInterval;
                }
            }

            using var md5 = MD5.Create();
            return md5.ComputeHash(combined.ToArray());
        }

        public static DirectoryCache Load(string directory)
        {
            CleanupStaleTemporaryFiles(directory);

            var cache = LoadBase(directory);
            LoadDelta(directory, cache);
            return cache;
        }

        private static DirectoryCache LoadBase(string directory)
        {
            var cache = new DirectoryCache();
            var path = GetCachePath(directory);
            if (!File.Exists(path))
            {
                return cache;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                var magic = reader.ReadBytes(Magic.Length);
                if (magic.Length != Magic.Length || !IsMagic(magic))
                {
                    return cache;
                }
                if (reader.ReadInt32() != 5)
                {
                    return cache;
                }

                var count = reader.ReadInt32();
                if (count < 0 || count > 100000)
                {
                    return cache;
                }

                for (int i = 0; i < count; i++)
                {
                    var id = reader.ReadBytes(IdLength);
                    if (id.Length != IdLength)
                    {
                        return cache;
                    }

                    var length = reader.ReadInt64();
                    var lastWriteTimeTicks = reader.ReadInt64();
                    var duration = reader.ReadDouble();
                    var frameCount = reader.ReadInt32();

                    // 由 Codex 修改：按缓存剩余长度校验帧数，不再限制为 10000 帧

                    var hashByteLength = frameCount < 0 ? -1L : (long)frameCount * HashLength;
                    if (hashByteLength < 0 || hashByteLength > int.MaxValue || hashByteLength > stream.Length - stream.Position)
                    {
                        return cache;
                    }

                    var hashes = new byte[(int)hashByteLength];
                    stream.ReadExactly(hashes);

                    cache.Add(new CacheEntry(id, length, lastWriteTimeTicks, duration, hashes));
                }
            }
            catch
            {
                // 缓存损坏时直接忽略，后续会重建
                return new DirectoryCache();
            }

            return cache;
        }

        private static void LoadDelta(string directory, DirectoryCache cache)
        {
            var path = GetDeltaPath(directory);
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
                using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                var originalLength = stream.Length;
                long lastValidPosition = 0;
                try
                {
                    if (originalLength == 0)
                    {
                        return;
                    }

                    if (originalLength < DeltaHeaderSize ||
                        !IsMagic(reader.ReadBytes(DeltaMagic.Length), DeltaMagic) ||
                        reader.ReadInt32() != DeltaVersion)
                    {
                        return;
                    }

                    lastValidPosition = DeltaHeaderSize;
                    while (stream.Position < originalLength)
                    {
                        var recordStart = stream.Position;
                        if (originalLength - recordStart < sizeof(int) + DeltaChecksumSize)
                        {
                            break;
                        }

                        var payloadLength = reader.ReadInt32();
                        if (payloadLength < DeltaPayloadFixedSize ||
                            payloadLength > originalLength - stream.Position - DeltaChecksumSize)
                        {
                            break;
                        }

                        var checksum = reader.ReadBytes(DeltaChecksumSize);
                        var payload = reader.ReadBytes(payloadLength);
                        if (checksum.Length != DeltaChecksumSize ||
                            payload.Length != payloadLength)
                        {
                            break;
                        }

                        var actualChecksum = SHA256.HashData(payload);
                        if (!CryptographicOperations.FixedTimeEquals(checksum, actualChecksum) ||
                            !TryReadDeltaEntry(payload, out var entry))
                        {
                            break;
                        }

                        cache.Add(entry);
                        lastValidPosition = stream.Position;
                    }
                }
                finally
                {
                    if (lastValidPosition < originalLength)
                    {
                        stream.SetLength(lastValidPosition);
                        stream.Flush(true);
                    }
                }
            }
            catch
            {
                // 增量日志读取失败时保留正式缓存，后续可以重新生成
            }
        }

        private static bool TryReadDeltaEntry(byte[] payload, out CacheEntry entry)
        {
            entry = null!;
            using var stream = new MemoryStream(payload, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            var id = reader.ReadBytes(IdLength);
            if (id.Length != IdLength || stream.Length - stream.Position < 28)
            {
                return false;
            }

            var length = reader.ReadInt64();
            var lastWriteTimeTicks = reader.ReadInt64();
            var duration = reader.ReadDouble();
            if (stream.Length - stream.Position < sizeof(int))
            {
                return false;
            }

            var frameCount = reader.ReadInt32();
            var hashByteLength = frameCount < 0 ? -1L : (long)frameCount * HashLength;
            if (hashByteLength < 0 || stream.Length - stream.Position != hashByteLength)
            {
                return false;
            }

            var hashes = reader.ReadBytes((int)hashByteLength);
            if (hashes.Length != hashByteLength)
            {
                return false;
            }

            entry = new CacheEntry(id, length, lastWriteTimeTicks, duration, hashes);
            return true;
        }

        public static void Append(string directory, CacheEntry entry)
        {
            if (entry.Hashes.Length % HashLength != 0 ||
                entry.Hashes.Length > int.MaxValue - DeltaPayloadFixedSize)
            {
                throw new InvalidDataException("pHashes 长度无效");
            }

            var payloadHeader = new byte[DeltaPayloadFixedSize];
            using (var payloadStream = new MemoryStream(payloadHeader))
            using (var payloadWriter = new BinaryWriter(payloadStream, Encoding.UTF8, true))
            {
                payloadWriter.Write(entry.Id);
                payloadWriter.Write(entry.Length);
                payloadWriter.Write(entry.LastWriteTimeTicks);
                payloadWriter.Write(entry.Duration);
                payloadWriter.Write(entry.Hashes.Length / HashLength);
                payloadWriter.Flush();
            }

            using var checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            checksum.AppendData(payloadHeader);
            checksum.AppendData(entry.Hashes);
            var checksumBytes = checksum.GetHashAndReset();

            var path = GetDeltaPath(directory);
            using var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                1024 * 1024,
                FileOptions.SequentialScan);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);

            if (stream.Length == 0)
            {
                writer.Write(DeltaMagic);
                writer.Write(DeltaVersion);
                writer.Flush();
                stream.Flush(true);
            }
            else
            {
                stream.Position = 0;
                using var headerReader = new BinaryReader(stream, Encoding.UTF8, true);
                if (stream.Length < DeltaHeaderSize ||
                    !IsMagic(headerReader.ReadBytes(DeltaMagic.Length), DeltaMagic) ||
                    headerReader.ReadInt32() != DeltaVersion)
                {
                    throw new InvalidDataException("pHashes 增量缓存版本无效");
                }
            }

            stream.Position = stream.Length;
            var recordStart = stream.Position;
            try
            {
                var payloadLength = DeltaPayloadFixedSize + entry.Hashes.Length;
                writer.Write(payloadLength);
                writer.Write(checksumBytes);
                writer.Write(payloadHeader);
                writer.Write(entry.Hashes);
                writer.Flush();
                stream.Flush(true);
            }
            catch
            {
                stream.SetLength(recordStart);
                stream.Flush(true);
                throw;
            }
        }

        public static void Delete(string directory)
        {
            File.Delete(GetCachePath(directory));
            File.Delete(GetDeltaPath(directory));
        }

        public static void Write(string directory, IEnumerable<CacheEntry> entries)
        {
            var path = GetCachePath(directory);
            var temporaryPath = Path.Combine(
                directory,
                $".phash.cache.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
            var list = entries as ICollection<CacheEntry> ?? new List<CacheEntry>(entries);

            // 由 Codex 修改：先写临时文件并落盘，再原子替换正式缓存

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 1024,
                    FileOptions.SequentialScan))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    {
                        writer.Write(Magic);
                        writer.Write(5);
                        writer.Write(list.Count);

                        foreach (var entry in list)
                        {
                            writer.Write(entry.Id);
                            writer.Write(entry.Length);
                            writer.Write(entry.LastWriteTimeTicks);
                            writer.Write(entry.Duration);

                            // 由 Codex 修改：连续哈希按帧数写入，保持原有缓存文件格式

                            if (entry.Hashes.Length % HashLength != 0)
                            {
                                throw new InvalidDataException("pHashes 长度不是单个哈希长度的整数倍");
                            }

                            writer.Write(entry.Hashes.Length / HashLength);
                            writer.Write(entry.Hashes);
                        }

                        writer.Flush();
                    }

                    stream.Flush(true);
                }

                File.Move(temporaryPath, path, true);
                File.Delete(GetDeltaPath(directory));
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        // 由 Codex 修改：清理异常退出遗留的旧临时缓存文件

        private static void CleanupStaleTemporaryFiles(string directory)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-1);
                foreach (var temporaryPath in Directory.EnumerateFiles(directory, ".phash.cache.*.tmp"))
                {
                    if (File.GetLastWriteTimeUtc(temporaryPath) < cutoff)
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
            catch
            {
                // 临时文件清理失败不影响正式缓存加载
            }
        }

        private static bool IsMagic(byte[] magic)
        {
            return IsMagic(magic, Magic);
        }

        private static bool IsMagic(byte[] magic, byte[] expected)
        {
            if (magic.Length != expected.Length)
            {
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (magic[i] != expected[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
#endregion
