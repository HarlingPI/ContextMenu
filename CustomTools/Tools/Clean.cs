using ConsoleKit;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CustomTools.Tools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/11/10 19:46:53
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks>清理</remarks>
    [MenuItem("文件清理", 2, Catgray.File)]
    public class Clean : ITool
    {
        public void Process(string path)
        {
            var search = Task.Run(() =>
            {
                return FileUtils.SearchFolders(path, int.MaxValue)
                .Reverse()
                .Where(f => FileUtils.SearchFiles(f).IsNullOrEmpty())
                .ToArray();
            });
            Effects.ShowSpinner2Char("Searching", search);

            var folders = search.Result;
            Console.WriteLine($"已搜索到空文件夹:{folders.Length}");
            if (folders.IsNullOrEmpty()) Console.WriteLine("本次运行不处理任何文件夹");
            else ClearEmptyFolders(path, folders);
        }

        private static void ClearEmptyFolders(string path, string[] folders)
        {
            Console.WriteLine("按任意键继续任务");
            Console.ReadKey();
            Ansi.ClearLastLine();

            //隐藏光标
            Ansi.HideCursor();
            //写入初始进度条
            Console.Write($"任务进度:{Effects.ProgressBar(40, 0)}({0}/{folders.Length})");

            for (int i = 0; i < folders.Length; i++)
            {
                //清除上一次的进度信息
                Ansi.ClearCurtLine();
                var folder = folders[i];

                Console.WriteLine(folder.Replace(path, "."));
                //更新进度条
                Console.Write($"任务进度:{Effects.ProgressBar(40, (i + 1f) / folders.Length)}({i + 1}/{folders.Length})");
                //删除文件夹
                FileUtils.DeleteFolder(folder);
            }
            Ansi.ShowCursor();
        }
    }
}