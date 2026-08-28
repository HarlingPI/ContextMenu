using ConsoleKit;
using PIToolKit.Public;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PIToolKit.Pool;

namespace CustomTools.Tools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/10/20 19:40:54
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks>文件归类工具</remarks>
    [MenuItem("文件归类", 0, Catgray.File)]
    public class Classify : ITool
    {
        private readonly ToolsConfig config;

        public Classify()
        {

            // 由Codex修改：从统一的配置文件读取分类规则
            config = ToolsConfig.LoadClassify();
        }


        public void Process(string path)
        {
            var search = Task.Run(() =>
            {
                var files = FileUtils.SearchFiles(path, greed: false).ToArray();
                var folders = FileUtils.SearchFolders(path, 1).ToArray();
                var pathparts = path.Split(new char[] { '\\', '/' })[1..];
                return (files, folders, pathparts);
            });
            Effects.ShowSpinner2Char("Searching", search);
            Console.WriteLine($"已搜索到文件夹:{search.Result.folders.Length},文件:{search.Result.files.Length}");

            var classfy = Task.Run(() =>
            {
                return ClassifyFiles(search.Result.files, search.Result.folders, search.Result.pathparts)
                .Where(kvp =>
                {
                    //挑选分类中数量大于1的文件夹进行创建,或者已经存在同名文件夹
                    return kvp.Value.Count > 1 ||
                    FileUtils.DirectoryIsExist(Path.Combine(path, kvp.Key));
                })
                .ToArray();
            });
            Effects.ShowSpinner2Char("Classfying", search);
            var groups = classfy.Result;
            var count = groups.Sum(g => g.Value.Count);
            Console.WriteLine($"涉及文件夹:{groups.Length}个,涉及文件:{count}个");

            Console.WriteLine("按任意键继续任务");
            Console.ReadKey();
            Ansi.ClearLastLine();

            if (groups.Length == 0) Console.WriteLine($"本次运行不处理任何文件!");
            else ProcessGroups(path, groups, count);
        }

        private static void ProcessGroups(string path, KeyValuePair<string, List<string>>[] groups, int count)
        {
            var counter = 0;
            //隐藏光标
            Ansi.HideCursor();
            //写入初始进度条
            Console.Write($"任务进度:{Effects.ProgressBar(40, 0)}({counter}/{count})");

            for (int i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                var folder = Path.Combine(path, group.Key);
                FileUtils.CreateFolder(folder);

                var files = group.Value;
                for (int j = 0; j < files.Count; j++)
                {
                    //清除上一次的进度信息
                    Ansi.ClearCurtLine();
                    var file = files[j];

                    Console.WriteLine(file);
                    //更新进度条
                    Console.Write($"任务进度:{Effects.ProgressBar(40, ++counter / (float)count)}({counter}/{count})");
                    //移动文件
                    var src = Path.Combine(path, file);
                    var dst = Path.Combine(folder, file);
                    FileUtils.MoveFile(src, dst, true);
                }
            }
            Ansi.ShowCursor();
        }

        private Dictionary<string, List<string>> ClassifyFiles(string[] files, string[] folders, string[] pathparts)
        {
            var groups = new Dictionary<string, List<string>>();

            // 由Codex修改：使用池化字典缓存文件夹名称，减少分类过程中的临时字典分配
            using var foldernames = new PooledDictionary<string, string>();
            for (int fi = 0; fi < folders.Length; fi++)
            {
                foldernames.Add(FileUtils.GetFolderName(folders[fi]), folders[fi]);
            }

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var name = FileUtils.GetFileName(file);
                string? folder = string.Empty;
                //通过尝试配置文件指定的关键词进行匹配
                if (folder.IsNullOrEmpty())
                {
                    foreach (var entry in config.Mapper)
                    {
                        if (name.Contains(entry.Key))
                        {
                            folder = entry.Value;
                            break;
                        }
                    }
                }
                //如果仍然没有匹配到，则在现有文件夹中进行模糊匹配
                if (folder.IsNullOrEmpty())
                {
                    folder = foldernames
                        .Where(kvp => name.Contains(kvp.Key))
                        .FirstOrDefault().Value;
                }
                //如果没有匹配到，通过正则表达式提取文件名前缀作为文件夹名称
                if (folder.IsNullOrEmpty())
                {
                    var matches = Regexs.Fixexp.Matches(name);
                    if (!matches.IsNullOrEmpty())
                    {
                        folder = matches
                            .Select(m => m.Value)
                            //忽略所有的纯数字前缀
                            .Where(v => !config.Ignores.Contains(v))
                            .Where(v => v.Length > 2)
                            .Where(v => !Regexs.Numexp.IsMatch(v[1..^1]))
                            .FirstOrDefault();
                    }
                }
                //忽略路径中已有的部分
                if (pathparts.Contains(folder)) folder = string.Empty;

                //最后如果还是没有匹配到，则跳过该文件
                if (folder.IsNullOrEmpty()) continue;
                if (!groups.TryGetValue(folder, out var list))
                {
                    list = new List<string>();
                    groups.Add(folder, list);
                }
                list.Add(name);
            }
            return groups;
        }
    }

    #region 由Codex添加
    /// <summary>
    /// 文件归类工具对应的配置分区
    /// </summary>
    public sealed partial class ToolsConfig
    {
        public List<string> Ignores = new List<string>();
        public Dictionary<string, string> Mapper = new Dictionary<string, string>();

        // 由Codex修改：改为解析扁平配置中的 classify 条目
        public static ToolsConfig LoadClassify()
        {
            var config = new ToolsConfig();
            var lines = FileUtils.ReadAllLines(ConfigPath);
            if (lines == null)
            {
                return config;
            }

            foreach (var line in lines)
            {
                var index = line.IndexOf(" = ", StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                var key = line[..index].Trim();
                var value = line[(index + 3)..];
                if (key == "classify.ignore")
                {
                    config.Ignores.Add(value);
                }
                else if (key == "classify.map")
                {
                    var sep = value.IndexOf(" | ", StringComparison.Ordinal);
                    if (sep >= 0)
                    {
                        config.Mapper.TryAdd(value[..sep], value[(sep + 3)..]);
                    }
                }
            }

            return config;
        }
    }
    #endregion
}
