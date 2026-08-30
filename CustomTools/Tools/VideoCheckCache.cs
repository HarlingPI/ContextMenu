// 本文件由 Codex 新增

#region 由 Codex 添加
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

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
        public byte[][] Hashes { get; }

        public CacheEntry(byte[] id, long length, long lastWriteTimeTicks, double duration, byte[][] hashes)
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
        private const int IdLength = 16;
        private const int HashLength = 32;
        private static readonly byte[] Magic = { (byte)'V', (byte)'C', (byte)'H', (byte)'K' };

        public static string GetCachePath(string directory)
        {
            return Path.Combine(directory, CacheFileName);
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
            using var md5 = MD5.Create();
            return md5.ComputeHash(stream);
        }

        public static DirectoryCache Load(string directory)
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
                if (reader.ReadInt32() != 4)
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
                    if (frameCount < 0 || frameCount > 10000)
                    {
                        return cache;
                    }

                    var hashes = new byte[frameCount][];
                    for (int f = 0; f < frameCount; f++)
                    {
                        var hash = reader.ReadBytes(HashLength);
                        if (hash.Length != HashLength)
                        {
                            return cache;
                        }
                        hashes[f] = hash;
                    }

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

        public static void Write(string directory, IEnumerable<CacheEntry> entries)
        {
            var path = GetCachePath(directory);
            var list = entries as ICollection<CacheEntry> ?? new List<CacheEntry>(entries);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write(Magic);
            writer.Write(4);
            writer.Write(list.Count);

            foreach (var entry in list)
            {
                writer.Write(entry.Id);
                writer.Write(entry.Length);
                writer.Write(entry.LastWriteTimeTicks);
                writer.Write(entry.Duration);
                writer.Write(entry.Hashes.Length);
                foreach (var hash in entry.Hashes)
                {
                    writer.Write(hash);
                }
            }
        }

        private static bool IsMagic(byte[] magic)
        {
            for (int i = 0; i < Magic.Length; i++)
            {
                if (magic[i] != Magic[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
#endregion
