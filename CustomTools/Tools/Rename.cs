using ConsoleKit;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CustomTools.Tools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/12/6 19:15:35
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    [MenuItem("名称清理", 2, Catgray.File)]
    public class Rename : ITool
    {
        private const string path = "Configs/.rename";
        private readonly HashSet<string> fixes;
        public Rename()
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
            var search = Task.Run(() => SearchFile(path));
            Effects.ShowSpinner2Char("Searching", search);
            Console.WriteLine($"涉及文件:{search.Result.Length}个");

            Console.WriteLine("按任意键继续任务");
            Console.ReadKey();
            Ansi.ClearLastLine();

            if (search.Result.Length == 0) Console.WriteLine($"本次运行不处理任何文件!");
            else RenameFiles(path, search.Result);
        }

        private static void RenameFiles(string path, (string file, string newname)[] files)
        {
            //隐藏光标
            Ansi.HideCursor();
            Console.Write($"任务进度:{Effects.ProgressBar(40, 0)}({1}/{files.Length})");

            for (int i = 0; i < files.Length; i++)
            {
                var (srcpath, newname) = files[i];
                //重命名文件
                //判断有没有目标文件
                var tarfullpath = Path.Combine(FileUtils.GetDirectory(srcpath), newname);
                var renameable = !FileUtils.FileIsExist(tarfullpath);
                if (!renameable)
                {
                    //如果已存在同名文件,判断hash值是否一致
                    var srchash = FileUtils.GetHashValue(srcpath);
                    var tarhash = FileUtils.GetHashValue(tarfullpath);

                    if (tarhash == srchash) renameable = true;

                    //如果hash值不一致，说明不是同一个文件,暂不处理
                }
                if (renameable) FileUtils.Rename(srcpath, newname, true);

                //清除上一次进度信息
                Ansi.ClearCurtLine();
                //输出文件名与重命名结果
                Console.Write($"{srcpath.Replace(path, ".")}\t{newname}");
                using (var scope = new ConsoleScope(renameable ? ConsoleColor.Green : ConsoleColor.Red))
                {
                    Console.WriteLine($"\t{renameable}");
                }
                //更新进度条
                var counter = i + 1;
                Console.Write($"任务进度:{Effects.ProgressBar(40, counter / (float)files.Length)}({counter}/{files.Length})");
            }
            Ansi.ShowCursor();
        }

        private (string file, string newname)[] SearchFile(string path)
        {
            return FileUtils.SearchFiles(path, greed: false)
                .Select(f =>
                {
                    var orgname = FileUtils.GetFileName(f);
                    var ext = FileUtils.GetExtension(orgname);
                    var newname = FileUtils.GetFileName(orgname, false);

                    //移除符合条件的部分
                    newname = Regexs.Fixexp.Replace(newname, string.Empty);
                    //再移除配置文件中的内容
                    foreach (var item in fixes)
                    {
                        newname = newname.Replace(item, string.Empty);
                    }
                    //移除前后的空格
                    newname = newname.Trim();
                    //拼接后缀名
                    newname += ext;

                    return (file: f, orgname, newname);
                })
                .Where(t => t.orgname != t.newname)
                .Select(t => (t.file, t.newname))
                .ToArray();
        }
    }
}