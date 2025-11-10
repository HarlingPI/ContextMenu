using ConsoleKit;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomTools.Tools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/11/1 16:10:25
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks>文件展平工具</remarks>
    [MenuItem("文件展平", 1, Catgray.File)]
    public class Flatten : ITool
    {
        public void Process(string path)
        {
            var search = Task.Run(() =>
            {
                return FileUtils.SearchFolders(path, 1)
                .ToDictionary(f => f, f => FileUtils.SearchFiles(f, greed: false)
                                                    .ToArray());
            });
            Effects.ShowSpinner2Char("Searching", search);
            var groups = search.Result;
            var count = groups.Sum(g => g.Value.Length);
            Console.WriteLine($"已搜索到文件夹:{groups.Count},文件:{count}");

            Console.WriteLine("按任意键继续任务");
            Console.ReadKey();
            Ansi.ClearLastLine();

            //隐藏光标
            Ansi.HideCursor();
            var counter = 0;
            Console.Write($"任务进度:{Effects.ProgressBar(40, 0)}({counter}/{count})");
            foreach (var kvp in groups)
            {
                var folder = kvp.Key;
                var files = kvp.Value;

                var foldername = FileUtils.GetFolderName(folder);
                if(!Regexs.Fixexp.IsMatch(foldername))
                {
                    foldername = $"[{foldername}]";
                }

                for (int i = 0; i < files.Length; i++)
                {
                    Ansi.ClearCurtLine();
                    var src = files[i];
                    var fielname = FileUtils.GetFileName(src);

                    Console.WriteLine(fielname);
                    Console.Write($"任务进度:{Effects.ProgressBar(40, (++counter) / (float)count)}({counter}/{count})");

                    //检查文件名中是否包含文件夹名称
                    if (!fielname.Contains(foldername))
                    {
                        fielname = $"{foldername} {fielname}";
                    }
                    var dst = Path.Combine(path, fielname);
                    //移动文件
                    FileUtils.MoveFile(src, dst, true);
                }
                //文件夹检查
                var remainfiles = FileUtils.SearchFiles(folder).ToArray();
                if (remainfiles.Length == 0)
                {
                    FileUtils.DeleteFolder(folder);
                }
            }
            Ansi.ShowCursor();
        }
    }
}