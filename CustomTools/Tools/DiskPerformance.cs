// 本文件由 Codex 新增：检测视频所在磁盘类型，用于选择不同的并行数量

#region 由 Codex 添加
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CustomTools.Tools
{
    /// <summary>
    /// 视频所在磁盘的介质类型。
    /// </summary>
    internal enum DiskMediaType
    {
        Unknown,
        Hdd,
        Ssd
    }

    /// <summary>
    /// 通过 Windows 存储设备属性判断卷所在磁盘是机械硬盘还是固态硬盘。
    /// </summary>
    internal static class DiskPerformance
    {
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint IoctlVolumeGetVolumeDiskExtents = 0x00560000;
        private const uint IoctlStorageQueryProperty = 0x002D1400;
        private const int StorageDeviceSeekPenaltyProperty = 7;
        private const int StorageDeviceTrimProperty = 8;
        private const int PropertyStandardQuery = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct DiskExtent
        {
            public uint DiskNumber;
            public uint Padding;
            public long StartingOffset;
            public long ExtentLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct VolumeDiskExtents
        {
            public uint NumberOfDiskExtents;
            public uint Padding;
            public DiskExtent Extent;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StoragePropertyQuery
        {
            public int PropertyId;
            public int QueryType;
            public byte AdditionalParameters;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceSeekPenaltyDescriptor
        {
            public uint Version;
            public uint Size;

            [MarshalAs(UnmanagedType.U1)]
            public bool IncursSeekPenalty;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DeviceTrimDescriptor
        {
            public uint Version;
            public uint Size;

            [MarshalAs(UnmanagedType.U1)]
            public bool TrimEnabled;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint ioControlCode,
            IntPtr inputBuffer,
            uint inputBufferSize,
            IntPtr outputBuffer,
            uint outputBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        private static extern bool DeviceIoControlSeekPenalty(
            SafeFileHandle device,
            uint ioControlCode,
            ref StoragePropertyQuery inputBuffer,
            uint inputBufferSize,
            out DeviceSeekPenaltyDescriptor outputBuffer,
            uint outputBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        private static extern bool DeviceIoControlTrim(
            SafeFileHandle device,
            uint ioControlCode,
            ref StoragePropertyQuery inputBuffer,
            uint inputBufferSize,
            out DeviceTrimDescriptor outputBuffer,
            uint outputBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        /// <summary>
        /// 根据目录所在卷检测磁盘类型，无法识别时返回未知。
        /// </summary>
        public static DiskMediaType Detect(string path)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path));
                if (string.IsNullOrEmpty(root) || root.Length < 2 || root[1] != ':')
                {
                    return DiskMediaType.Unknown;
                }

                var driveLetter = char.ToUpperInvariant(root[0]);
                using var volume = OpenDevice($@"\\.\{driveLetter}:");
                if (volume.IsInvalid || !TryGetDiskNumber(volume, out var diskNumber))
                {
                    return DiskMediaType.Unknown;
                }

                using var physicalDisk = OpenDevice($@"\\.\PhysicalDrive{diskNumber}");
                if (physicalDisk.IsInvalid)
                {
                    return DiskMediaType.Unknown;
                }

                if (TryGetSeekPenalty(physicalDisk, out var incursSeekPenalty))
                {
                    return incursSeekPenalty ? DiskMediaType.Hdd : DiskMediaType.Ssd;
                }

                // 部分虚拟盘或 USB 设备不支持寻道惩罚属性，再尝试 TRIM 属性。
                if (TryGetTrimSupport(physicalDisk, out var trimEnabled))
                {
                    return trimEnabled ? DiskMediaType.Ssd : DiskMediaType.Hdd;
                }
            }
            catch
            {
                // 检测失败时由调用方回退到默认并行数。
            }

            return DiskMediaType.Unknown;
        }

        private static SafeFileHandle OpenDevice(string path)
        {
            return CreateFile(
                path,
                0,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);
        }

        private static bool TryGetDiskNumber(SafeFileHandle volume, out uint diskNumber)
        {
            diskNumber = 0;
            const int bufferSize = 1024;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                if (!DeviceIoControl(
                    volume,
                    IoctlVolumeGetVolumeDiskExtents,
                    IntPtr.Zero,
                    0,
                    buffer,
                    bufferSize,
                    out _,
                    IntPtr.Zero))
                {
                    return false;
                }

                diskNumber = (uint)Marshal.ReadInt32(buffer, 8);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool TryGetSeekPenalty(SafeFileHandle physicalDisk, out bool incursSeekPenalty)
        {
            var query = new StoragePropertyQuery
            {
                PropertyId = StorageDeviceSeekPenaltyProperty,
                QueryType = PropertyStandardQuery
            };

            var succeeded = DeviceIoControlSeekPenalty(
                physicalDisk,
                IoctlStorageQueryProperty,
                ref query,
                (uint)Marshal.SizeOf<StoragePropertyQuery>(),
                out var descriptor,
                (uint)Marshal.SizeOf<DeviceSeekPenaltyDescriptor>(),
                out _,
                IntPtr.Zero);

            incursSeekPenalty = descriptor.IncursSeekPenalty;
            return succeeded;
        }

        private static bool TryGetTrimSupport(SafeFileHandle physicalDisk, out bool trimEnabled)
        {
            var query = new StoragePropertyQuery
            {
                PropertyId = StorageDeviceTrimProperty,
                QueryType = PropertyStandardQuery
            };

            var succeeded = DeviceIoControlTrim(
                physicalDisk,
                IoctlStorageQueryProperty,
                ref query,
                (uint)Marshal.SizeOf<StoragePropertyQuery>(),
                out var descriptor,
                (uint)Marshal.SizeOf<DeviceTrimDescriptor>(),
                out _,
                IntPtr.Zero);

            trimEnabled = descriptor.TrimEnabled;
            return succeeded;
        }
    }
}
#endregion
