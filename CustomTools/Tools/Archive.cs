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

            ////为多个具有相同前缀的文件创建文件夹，并移动到新文件夹中
            //var temp = files
            //    .Select((f) => (Expes.fixexp.Match(f, 0).Value, f))
            //    .Concat(files
            //            .Where(f => f.Contains("Pornhub"))
            //            .Select(f => ("Pornhub", f)))
            //    .Concat(files
            //            .Where(f => f.Contains("完整视频"))
            //            .Select(f => ("完整视频", f)))
            //    .Concat(files
            //            .Where(f => f.Contains("Rule 34"))
            //            .Select(f => ("Rule 34", f)))
            //    .Concat(files
            //            .Where(f => f.Contains("中文音声"))
            //            .Select(f => ("中文音声", f)))
            //    //过滤已在文件夹内的文件
            //    .Where(t =>
            //    {
            //        var folder = FileUtils.GetFolderName(t.Item2);
            //        return t.Item1 != folder;
            //    })
            //    .GroupBy((t) => t.Item1)
            //    .Where((g) => !string.IsNullOrEmpty(g.Key))
            //    .Where((g) => g.Count() >= 2)
            //    .ToArray();

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