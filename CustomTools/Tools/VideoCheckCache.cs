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
        public double Duration { get; }
        public byte[][] Hashes { get; }

        public CacheEntry(byte[] id, double duration, byte[][] hashes)
        {
            Id = id;
            Duration = duration;
            Hashes = hashes;
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

        public static byte[] ComputeContentId(string file)
        {
            using var stream = File.OpenRead(file);
            using var md5 = MD5.Create();
            return md5.ComputeHash(stream);
        }

        public static Dictionary<string, CacheEntry> Load(string directory)
        {
            var entries = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
            var path = GetCachePath(directory);
            if (!File.Exists(path))
            {
                return entries;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                var magic = reader.ReadBytes(Magic.Length);
                if (magic.Length != Magic.Length || !IsMagic(magic))
                {
                    return entries;
                }
                if (reader.ReadInt32() != 2)
                {
                    return entries;
                }

                var count = reader.ReadInt32();
                if (count < 0 || count > 100000)
                {
                    return entries;
                }

                for (int i = 0; i < count; i++)
                {
                    var id = reader.ReadBytes(IdLength);
                    if (id.Length != IdLength)
                    {
                        return entries;
                    }

                    var duration = reader.ReadDouble();
                    var frameCount = reader.ReadInt32();
                    if (frameCount < 0 || frameCount > 10000)
                    {
                        return entries;
                    }

                    var hashes = new byte[frameCount][];
                    for (int f = 0; f < frameCount; f++)
                    {
                        var hash = reader.ReadBytes(HashLength);
                        if (hash.Length != HashLength)
                        {
                            return entries;
                        }
                        hashes[f] = hash;
                    }

                    entries[ToKey(id)] = new CacheEntry(id, duration, hashes);
                }
            }
            catch
            {
                // 缓存损坏时直接忽略，后续会重建
                return new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
            }

            return entries;
        }

        public static void Write(string directory, IEnumerable<CacheEntry> entries)
        {
            var path = GetCachePath(directory);
            var list = entries as ICollection<CacheEntry> ?? new List<CacheEntry>(entries);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write(Magic);
            writer.Write(2);
            writer.Write(list.Count);

            foreach (var entry in list)
            {
                writer.Write(entry.Id);
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
