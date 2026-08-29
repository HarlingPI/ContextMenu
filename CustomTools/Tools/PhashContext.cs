// 本文件由 Codex 新增

#region 由 Codex 添加
using System;
using System.Runtime.CompilerServices;

namespace CustomTools.Tools
{
    /// <summary>
    /// pHash 计算上下文，复用临时数组以避免每帧重复分配内存。
    /// 算法参考 VideoPrint 项目：先计算 32x32 DCT，再取左上角 16x16 低频系数中位数生成 256 位哈希。
    /// </summary>
    internal sealed class PhashContext
    {
        private const int FeatureSize = 32;
        private const int BlockSize = 16;

        private readonly byte[] grayData = new byte[FeatureSize * FeatureSize];
        private readonly double[] dctIn = new double[FeatureSize];
        private readonly double[] dctOut = new double[FeatureSize];
        private readonly double[] dctTemp = new double[FeatureSize * FeatureSize];
        private readonly double[] dctResult = new double[FeatureSize * FeatureSize];
        private readonly double[] coeffs = new double[BlockSize * BlockSize];
        private readonly double[] sorted = new double[BlockSize * BlockSize];
        private readonly byte[] hashBuffer = new byte[BlockSize * BlockSize / 8];
        private readonly double[,] cosTable = CreateCosTable();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ComputeHash(byte[] gray)
        {
            // ffmpeg 已输出 32x32 灰度帧，直接拷贝即可
            Buffer.BlockCopy(gray, 0, grayData, 0, grayData.Length);

            // 行方向 DCT
            for (int y = 0; y < FeatureSize; y++)
            {
                int rowBase = y * FeatureSize;
                for (int x = 0; x < FeatureSize; x++)
                {
                    dctIn[x] = grayData[rowBase + x] / 255.0;
                }
                Dct1D(dctIn, dctOut, cosTable);
                for (int x = 0; x < FeatureSize; x++)
                {
                    dctTemp[rowBase + x] = dctOut[x];
                }
            }

            // 列方向 DCT
            for (int x = 0; x < FeatureSize; x++)
            {
                for (int y = 0; y < FeatureSize; y++)
                {
                    dctIn[y] = dctTemp[y * FeatureSize + x];
                }
                Dct1D(dctIn, dctOut, cosTable);
                for (int y = 0; y < FeatureSize; y++)
                {
                    dctResult[y * FeatureSize + x] = dctOut[y];
                }
            }

            // 取左上角低频系数并计算中位数
            Array.Copy(dctResult, coeffs, coeffs.Length);
            Array.Copy(coeffs, sorted, sorted.Length);
            Array.Sort(sorted);
            double median = sorted[sorted.Length / 2];

            // 将高频系数位标记为 1，得到 256 位哈希
            Array.Clear(hashBuffer, 0, hashBuffer.Length);
            for (int i = 0; i < coeffs.Length; i++)
            {
                if (coeffs[i] > median)
                {
                    hashBuffer[i / 8] |= (byte)(1 << (7 - (i % 8)));
                }
            }

            return (byte[])hashBuffer.Clone();
        }

        private static void Dct1D(double[] input, double[] output, double[,] cosTable)
        {
            for (int k = 0; k < FeatureSize; k++)
            {
                double sum = 0;
                for (int n = 0; n < FeatureSize; n++)
                {
                    sum += input[n] * cosTable[n, k];
                }
                output[k] = sum;
            }
        }

        private static double[,] CreateCosTable()
        {
            const int n = FeatureSize;
            var table = new double[n, n];
            double factor = Math.PI / (2 * n);
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < n; k++)
                {
                    table[i, k] = Math.Cos(factor * (2 * i + 1) * k);
                }
            }
            return table;
        }
    }
}
#endregion
