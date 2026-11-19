// 本文件由 GPT-5 新增：图片查重内容型 pHash 目录缓存

#region 由 GPT-5 添加
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CustomTools.Tools
{
    /// <summary>
    /// 单张图片的 pHash 缓存记录，以内容 ID 和文件签名定位，与文件名无关。
    /// </summary>
    internal sealed class ImageCacheEntry
    {
        public byte[] Id { get; }
        public long Length { get; }
        public long LastWriteTimeTicks { get; }
        public byte[] Hash { get; }

        public ImageCacheEntry(byte[] id, long length, long lastWriteTimeTicks, byte[] hash)
        {
            Id = id;
            Length = length;
            LastWriteTimeTicks = lastWriteTimeTicks;
            Hash = hash;
        }
    }

    /// <summary>
    /// 单个目录内的图片 pHash 缓存，按内容 ID 保存，并建立文件签名索引。
    /// </summary>
    internal sealed class ImageDirectoryCache
    {
        private readonly Dictionary<string, ImageCacheEntry> byId =
            new Dictionary<string, ImageCacheEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ImageCacheEntry>> bySignature =
            new Dictionary<string, List<ImageCacheEntry>>(StringComparer.Ordinal);

        public int Count => byId.Count;
        public IReadOnlyCollection<ImageCacheEntry> Entries => byId.Values;

        public bool TryGetUniqueBySignature(FileInfo info, out ImageCacheEntry entry)
        {
            var signature = ImageCheckCache.SignatureKey(info.Length, info.LastWriteTimeUtc.Ticks);
            if (bySignature.TryGetValue(signature, out var entries) && entries.Count == 1)
            {
                entry = entries[0];
                return true;
            }

            entry = null!;
            return false;
        }

        public bool TryGetById(byte[] id, out ImageCacheEntry entry)
        {
            return byId.TryGetValue(ImageCheckCache.ToKey(id), out entry!);
        }

        public void Add(ImageCacheEntry entry)
        {
            var idKey = ImageCheckCache.ToKey(entry.Id);
            if (byId.TryGetValue(idKey, out var previous))
            {
                RemoveFromSignature(
                    idKey,
                    ImageCheckCache.SignatureKey(
                        previous.Length,
                        previous.LastWriteTimeTicks));
            }

            byId[idKey] = entry;
            var signature = ImageCheckCache.SignatureKey(entry.Length, entry.LastWriteTimeTicks);
            if (!bySignature.TryGetValue(signature, out var entries))
            {
                entries = new List<ImageCacheEntry>();
                bySignature[signature] = entries;
            }
            entries.Add(entry);
        }

        public ImageCacheEntry[] FindStaleEntries(HashSet<string> activeIds)
        {
            return byId
                .Where(pair => !activeIds.Contains(pair.Key))
                .Select(pair => pair.Value)
                .ToArray();
        }

        public bool RemoveById(string idKey)
        {
            if (!byId.TryGetValue(idKey, out var entry))
            {
                return false;
            }

            RemoveFromSignature(
                idKey,
                ImageCheckCache.SignatureKey(
                    entry.Length,
                    entry.LastWriteTimeTicks));
            return byId.Remove(idKey);
        }

        private void RemoveFromSignature(string idKey, string signature)
        {
            if (!bySignature.TryGetValue(signature, out var entries))
            {
                return;
            }

            entries.RemoveAll(entry => ImageCheckCache.ToKey(entry.Id) == idKey);
            if (entries.Count == 0)
            {
                bySignature.Remove(signature);
            }
        }
    }

    /// <summary>
    /// 图片 pHash 缓存的读写、内容 ID 计算与完整性校验。
    /// </summary>
    internal static class ImageCheckCache
    {
        private const string CacheFileName = ".image.phash.cache";
        private const string DeltaFileName = ".image.phash.cache.delta";
        private const int CacheVersion = 2;
        private const int DeltaVersion = 1;
        private const int DeltaHeaderSize = 8;
        private const int DeltaChecksumSize = 32;
        private const int DeltaEntryFixedSize = IdLength + sizeof(long) + sizeof(long);
        private const int MaximumEntryCount = 1_000_000;
        private static readonly byte[] Magic = { (byte)'I', (byte)'P', (byte)'H', (byte)'C' };
        private static readonly byte[] DeltaMagic = { (byte)'I', (byte)'P', (byte)'D', (byte)'J' };

        public const int IdLength = 32;

        public static string GetCachePath(string directory)
        {
            return Path.Combine(directory, CacheFileName);
        }

        public static string GetDeltaPath(string directory)
        {
            return Path.Combine(directory, DeltaFileName);
        }

        public static bool HasDelta(string directory)
        {
            return File.Exists(GetDeltaPath(directory));
        }

        public static bool HasCacheFiles(string directory)
        {
            if (File.Exists(GetCachePath(directory)) ||
                File.Exists(GetDeltaPath(directory)))
            {
                return true;
            }

            try
            {
                return Directory.EnumerateFiles(directory, ".image.phash.cache.*.tmp").Any();
            }
            catch
            {
                return false;
            }
        }

        public static string ToKey(byte[] id)
        {
            return Convert.ToHexString(id);
        }

        public static string SignatureKey(long length, long lastWriteTimeTicks)
        {
            return $"{length}:{lastWriteTimeTicks}";
        }

        public static byte[] ComputeContentId(string file)
        {
            using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                1024 * 1024,
                FileOptions.SequentialScan);
            return SHA256.HashData(stream);
        }

        public static ImageDirectoryCache Load(string directory)
        {
            CleanupStaleTemporaryFiles(directory);
            var cache = LoadBase(directory);
            LoadDelta(directory, cache);
            return cache;
        }

        private static ImageDirectoryCache LoadBase(string directory)
        {
            var path = GetCachePath(directory);
            var cache = new ImageDirectoryCache();
            if (!File.Exists(path))
            {
                return cache;
            }

            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.SequentialScan);
                using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                if (!IsMagic(reader.ReadBytes(Magic.Length)) ||
                    reader.ReadInt32() != CacheVersion ||
                    reader.ReadInt32() != IdLength ||
                    reader.ReadInt32() != PhashContext.HashLength)
                {
                    return new ImageDirectoryCache();
                }

                var count = reader.ReadInt32();
                if (count < 0 || count > MaximumEntryCount)
                {
                    return new ImageDirectoryCache();
                }

                for (var i = 0; i < count; i++)
                {
                    var id = reader.ReadBytes(IdLength);
                    var length = reader.ReadInt64();
                    var lastWriteTimeTicks = reader.ReadInt64();
                    var hash = reader.ReadBytes(PhashContext.HashLength);
                    if (id.Length != IdLength ||
                        length < 0 ||
                        hash.Length != PhashContext.HashLength)
                    {
                        return new ImageDirectoryCache();
                    }

                    cache.Add(new ImageCacheEntry(id, length, lastWriteTimeTicks, hash));
                }
            }
            catch
            {
                // 缓存损坏时忽略旧内容，本轮会重新计算并覆盖
                return new ImageDirectoryCache();
            }

            return cache;
        }

        private static void LoadDelta(string directory, ImageDirectoryCache cache)
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
                        if (originalLength - stream.Position < sizeof(int) + DeltaChecksumSize)
                        {
                            break;
                        }

                        var payloadLengthField = reader.ReadInt32();
                        var isDeletion = payloadLengthField < 0;
                        var payloadLength = isDeletion
                            ? -(long)payloadLengthField
                            : payloadLengthField;
                        var minimumPayloadLength = isDeletion
                            ? IdLength
                            : DeltaEntryFixedSize + PhashContext.HashLength;
                        if (payloadLength < minimumPayloadLength ||
                            payloadLength > originalLength - stream.Position - DeltaChecksumSize)
                        {
                            break;
                        }

                        var checksum = reader.ReadBytes(DeltaChecksumSize);
                        var payload = reader.ReadBytes((int)payloadLength);
                        if (checksum.Length != DeltaChecksumSize ||
                            payload.Length != payloadLength ||
                            !CryptographicOperations.FixedTimeEquals(
                                checksum,
                                SHA256.HashData(payload)))
                        {
                            break;
                        }

                        if (isDeletion)
                        {
                            if (!TryReadDeltaDeletion(payload, out var deletedId))
                            {
                                break;
                            }
                            cache.RemoveById(ToKey(deletedId));
                        }
                        else
                        {
                            if (!TryReadDeltaEntry(payload, out var entry))
                            {
                                break;
                            }
                            cache.Add(entry);
                        }
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
                // 增量日志损坏时保留正式缓存，后续可以重新生成
            }
        }

        private static bool TryReadDeltaDeletion(byte[] payload, out byte[] id)
        {
            id = Array.Empty<byte>();
            if (payload.Length != IdLength)
            {
                return false;
            }

            id = payload;
            return true;
        }

        private static bool TryReadDeltaEntry(byte[] payload, out ImageCacheEntry entry)
        {
            entry = null!;
            if (payload.Length != DeltaEntryFixedSize + PhashContext.HashLength)
            {
                return false;
            }

            using var stream = new MemoryStream(payload, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            var id = reader.ReadBytes(IdLength);
            var length = reader.ReadInt64();
            var lastWriteTimeTicks = reader.ReadInt64();
            var hash = reader.ReadBytes(PhashContext.HashLength);
            if (id.Length != IdLength ||
                length < 0 ||
                hash.Length != PhashContext.HashLength)
            {
                return false;
            }

            entry = new ImageCacheEntry(id, length, lastWriteTimeTicks, hash);
            return true;
        }

        public static void Append(string directory, ImageCacheEntry entry)
        {
            if (entry.Id.Length != IdLength ||
                entry.Hash.Length != PhashContext.HashLength)
            {
                throw new InvalidDataException("图片 pHash 增量记录无效");
            }

            using var payload = new MemoryStream(DeltaEntryFixedSize);
            using (var writer = new BinaryWriter(payload, Encoding.UTF8, true))
            {
                writer.Write(entry.Id);
                writer.Write(entry.Length);
                writer.Write(entry.LastWriteTimeTicks);
                writer.Write(entry.Hash);
                writer.Flush();
            }

            AppendDeltaRecord(directory, payload.ToArray(), false);
        }

        public static void AppendDeletion(string directory, byte[] id)
        {
            if (id.Length != IdLength)
            {
                throw new InvalidDataException("图片 pHash 删除记录无效");
            }

            AppendDeltaRecord(directory, id, true);
        }

        private static void AppendDeltaRecord(
            string directory,
            byte[] payload,
            bool isDeletion)
        {
            var checksum = SHA256.HashData(payload);
            var path = GetDeltaPath(directory);
            using var stream = new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.Read,
                64 * 1024,
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
                using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                if (stream.Length < DeltaHeaderSize ||
                    !IsMagic(reader.ReadBytes(DeltaMagic.Length), DeltaMagic) ||
                    reader.ReadInt32() != DeltaVersion)
                {
                    throw new InvalidDataException("图片 pHash 增量缓存版本无效");
                }
            }

            stream.Position = stream.Length;
            var recordStart = stream.Position;
            try
            {
                writer.Write(isDeletion ? -payload.Length : payload.Length);
                writer.Write(checksum);
                writer.Write(payload);
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

        public static void Write(string directory, IEnumerable<ImageCacheEntry> entries)
        {
            var list = entries.ToArray();
            var temporaryPath = Path.Combine(
                directory,
                $".image.phash.cache.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.SequentialScan))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                    {
                        writer.Write(Magic);
                        writer.Write(CacheVersion);
                        writer.Write(IdLength);
                        writer.Write(PhashContext.HashLength);
                        writer.Write(list.Length);
                        foreach (var entry in list)
                        {
                            if (entry.Id.Length != IdLength ||
                                entry.Hash.Length != PhashContext.HashLength)
                            {
                                throw new InvalidDataException("图片 pHash 缓存记录无效");
                            }

                            writer.Write(entry.Id);
                            writer.Write(entry.Length);
                            writer.Write(entry.LastWriteTimeTicks);
                            writer.Write(entry.Hash);
                        }
                        writer.Flush();
                    }
                    stream.Flush(true);
                }

                File.Move(temporaryPath, GetCachePath(directory), true);
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

        public static void Delete(string directory)
        {
            File.Delete(GetCachePath(directory));
            File.Delete(GetDeltaPath(directory));
        }

        public static void DeleteOrphanFiles(string directory)
        {
            File.Delete(GetCachePath(directory));
            File.Delete(GetDeltaPath(directory));
            foreach (var path in Directory.EnumerateFiles(directory, ".image.phash.cache.*.tmp"))
            {
                File.Delete(path);
            }
        }

        private static void CleanupStaleTemporaryFiles(string directory)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-1);
                foreach (var path in Directory.EnumerateFiles(directory, ".image.phash.cache.*.tmp"))
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                    {
                        File.Delete(path);
                    }
                }
            }
            catch
            {
                // 临时文件清理失败不影响缓存读取
            }
        }

        private static bool IsMagic(byte[] value)
        {
            return IsMagic(value, Magic);
        }

        private static bool IsMagic(byte[] value, byte[] expected)
        {
            if (value.Length != expected.Length)
            {
                return false;
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (value[i] != expected[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
#endregion
