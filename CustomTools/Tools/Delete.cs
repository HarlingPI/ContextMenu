using ConsoleKit;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CustomTools.Tools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2026/3/22 16:47:16
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    [MenuItem("文件清理", 3, Catgray.File)]
    public class Delete : ITool
    {
        public const string path = "Configs/.rename";
        private readonly HashSet<string> fixes;
        public Delete()
        {
            fixes = new HashSet<string>();
            var array = FileUtils.ReadAllLines(path);
            if (array.NotNullOrEmpty())
            {
                foreach (var item in array)
                {
                    fixes.Add(item);
                }
            }
        }
        public void Process(string path)
        {
            //查找文件夹下的所有同名文件
            var search = Task.Run(() =>
            {
                var files = FileUtils.SearchFiles(path).ToArray();
                return files.GroupBy(f =>
                {
                    var file = FileUtils.GetFileName(f, false);
                    var ext = FileUtils.GetExtension(f);
                    file = Regexs.Fixexp.Replace(file, string.Empty);
                    file = Regexs.IDMmark.Replace(file, string.Empty);

                    foreach (var item in fixes)
                    {
                        file = file.Replace(item, string.Empty);
                    }
                    using var handle = File.OpenHandle(f);
                    var length = RandomAccess.GetLength(handle);
                    //用文件名和长度作为判断相等的依据
                    return (file.Trim() + ext, length);
                })
                //将文件路径按长度倒序排列
                .Select(g => (g.Key, g.OrderByDescending(f => f.Length).ToArray()))
                .Where(g => g.Item2.Length > 1)
                .ToArray();
            });
            Effects.ShowSpinner2Char("Searching", search);
            Console.WriteLine($"已搜索到同名文件:{search.Result.Length}");

            Console.WriteLine("按任意键继续任务");
            Console.ReadKey();
            Ansi.ClearLastLine();

            //隐藏光标
            Ansi.HideCursor();
            //通过Hash值比较文件内容
            var groups = search.Result;
            for (int i = 0; i < groups.Length; i++)
            {
                var (key, files) = groups[i];
                //清除上一次的进度信息
                Ansi.ClearCurtLine();
                Console.WriteLine(key);
                //更新进度条
                Console.Write($"任务进度:{Effects.ProgressBar(40, (i + 1f) / groups.Length)}({(i + 1)}/{groups.Length})");
                for (int j = 1; j < files.Length; j++)
                {
                    FileUtils.DeleteFile(files[j]);
                }
            }
            Ansi.ShowCursor();
        }
    }
}