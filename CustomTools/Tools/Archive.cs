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
            //Effects.ShowSpinner2Char("Searching", task);

            var progress = 0f;
            while (progress <= 1f)
            {
                Console.WriteLine($"{Effects.ProgressBar(40, progress)}--Progress:{progress:P4}");
                //Console.Write(result);
                Thread.Sleep(100);
                //Ansi.ClearCurtLine();
                progress += 0.01f;
            }
            Console.WriteLine($"{Effects.ProgressBar(40, progress = 1)}--Progress:{progress:P4}");

            //Console.WriteLine($"已检索到{task.Result.Length}个文件");

            Console.WriteLine(" ▏▎▍▌▋▊▉█");
            Console.WriteLine("◐ ◓ ◑ ◒ ◐ ◓");
        }
    }
}