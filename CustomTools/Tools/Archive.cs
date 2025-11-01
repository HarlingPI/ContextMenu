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
            var task = Task.Run(() =>
            {
                return FileUtils.SearchFiles(path, greed: false);
            });
            Effects.ShowSpinner2Char("Searching", task);

            //var files = FileUtils.SearchFiles("D:\\Projects\\Converter", greed: true);
            //foreach (var item in files)
            //{
            //    Console.WriteLine(item);
            //}

            //Console.WriteLine($"已检索到{task.Result.Length}个文件");

            Console.WriteLine(" ▏▎▍▌▋▊▉█");
            Console.WriteLine("◐ ◓ ◑ ◒ ◐ ◓");
        }
    }
}