using ConsoleKit;
using PIToolKit.Public;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

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
        private readonly Dictionary<string, string> config = new Dictionary<string, string>();
        public Classify()
        {
            var path = "Configs/Classify.xml";
            if (FileUtils.FileIsExist(path))
            {
                KeyEntry[] array = null;
                XmlUtils.DeSerializeFromXmlFile(path, ref array);
                if (!array.IsNullOrEmpty())
                {
                    foreach (var item in array)
                    {
                        config.TryAdd(item.Key, item.Folder);
                    }
                }
            }
        }

        public void Process(string path)
        {
            var search = Task.Run(() =>
            {
                var files = FileUtils.SearchFiles(path, greed: false).ToArray();
                var folders = FileUtils.SearchFolders(path, 1, false).ToArray();
                return (files, folders);
            });
            Effects.ShowSpinner2Char("Searching", search);
            Console.WriteLine($"已搜索到文件夹:{search.Result.folders.Length},文件:{search.Result.files.Length}");

            var classfy = Task.Run(() =>
            {
                return ClassifyFiles(search.Result.files, search.Result.folders)
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

        private Dictionary<string, List<string>> ClassifyFiles(string[] files, string[] folders)
        {
            var groups = new Dictionary<string, List<string>>();
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var name = FileUtils.GetFileName(file);
                string? folder = string.Empty;
                //通过正则表达式提取文件名前缀作为文件夹名称
                var matches = Regexs.Fixexp.Matches(name);
                if (!matches.IsNullOrEmpty())
                {
                    folder = matches
                        .Select(m => m.Value)
                        .Where(v => !int.TryParse(v[1..^1], out _))
                        .Where(v => !v[1..^1].Equals("3d", StringComparison.CurrentCultureIgnoreCase))
                        .Where(v => !v[1..^1].Equals("4k", StringComparison.CurrentCultureIgnoreCase))
                        .FirstOrDefault();
                }
                //如果没有匹配到，则通过尝试配置文件指定的关键词进行匹配
                if (folder.IsNullOrEmpty())
                {
                    foreach (var entry in config)
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
                    folder = folders
                        .Where(name.Contains)
                        .FirstOrDefault();
                }
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

        [Serializable]
        public struct KeyEntry
        {
            [XmlAttribute]
            public string Key;
            [XmlAttribute]
            public string Folder;

            public KeyEntry(string key, string folder)
            {
                Key = key;
                Folder = folder;
            }
        }
    }
}