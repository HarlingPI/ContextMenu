using ConsoleKit;
using PIToolKit.Public;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomTools.Tools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/10/20 19:40:54
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks>文件归类工具</remarks>
    [MenuItem("文件归类", "Classify", 0, 1)]
    public class Archive : ITool
    {
        public void Process(string path)
        {
            //string[] files = null;
            //var time = PublicUtility.ReckonTime(() =>
            //{
            //    files = FileUtils.SearchFiles(path).ToArray();
            //});

            var task = Task.Run(async () =>
            {
                for (int i = 0; i < 100; i++)
                {
                    await Task.Delay(100);
                }
                return FileUtils.SearchFiles(path).ToArray();
            });

            var symbols = "⣷⣯⣟⡿⢿⣻⣽⣾";
            var idx = 0;
            while (!task.IsCompleted)
            {
                Console.Write(symbols[idx++]);
                Thread.Sleep(125);
                VirtualTerminal.ClearLastChar();
                idx %= symbols.Length;
            }
            Console.WriteLine($"已检索到{task.Result.Length}个文件");


            Console.WriteLine("█▉▊▋▌▍▎▏");
            Console.WriteLine("⣷ ⣯ ⣟ ⡿ ⢿ ⣻ ⣽ ⣾");
            Console.WriteLine("◐ ◓ ◑ ◒ ◐ ◓");
        }
    }
}