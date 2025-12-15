using ConsoleKit;
using PIToolKit.Public;
using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
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
        private const string path = "Configs/.classify";
        private readonly Config config = null;
        public Classify()
        {
            config = ReadConfig(path);
        }


        public void Process(string path)
        {
            var search = Task.Run(() =>
            {
                var files = FileUtils.SearchFiles(path, greed: false).ToArray();
                var folders = FileUtils.SearchFolders(path, 1).ToArray();
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

        private Dictionary<string, List<string>> ClassifyFiles(string[] files, string[] folders)
        {
            var groups = new Dictionary<string, List<string>>();
            var foldernames = folders.ToDictionary(FileUtils.GetFolderName, f => f);
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
                        //忽略所有的纯数字前缀
                        folder = matches
                            .Select(m => m.Value)
                            .Where(v => !config.Ignores.Contains(v))
                            .Where(v => v.Length > 2)
                            .Where(v => !Regexs.Numexp.IsMatch(v[1..^1]))
                            .FirstOrDefault();
                    }
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
        private static void SaveConfig(string path, Config config)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("Config");
            doc.AppendChild(root);

            var ignores = root.CreateChild("Ignores");
            foreach (var item in config.Ignores)
            {
                ignores.CreateChild("Item").CreateAttribute("V").Value = item;
            }

            var mapper = root.CreateChild("Mapper");
            foreach (var kvp in config.Mapper)
            {
                var item = mapper.CreateChild("Item");
                item.CreateAttribute("K").Value = kvp.Key;
                item.CreateAttribute("V").Value = kvp.Value;
            }

            doc.Save(path);
        }
        private static Config ReadConfig(string path)
        {
            var config = new Config();

            if (FileUtils.FileIsExist(path))
            {
                var temp = new XmlDocument();
                temp.Load(path);

                var root = temp.DocumentElement;

                var ignores = root.SelectSingleNode("./Ignores");
                if (ignores != null)
                {
                    foreach (XmlNode item in ignores.ChildNodes)
                    {
                        config.Ignores.Add(item.Attributes["V"].Value);
                    }
                }

                var mapper = root.SelectSingleNode("./Mapper");
                if (mapper != null)
                {
                    foreach (XmlNode item in mapper.ChildNodes)
                    {
                        config.Mapper.TryAdd(item.Attributes["K"].Value, item.Attributes["V"].Value);
                    }
                }
            }

            return config;
        }
        private class Config
        {
            public List<string> Ignores = new List<string>();

            public Dictionary<string, string> Mapper = new Dictionary<string, string>();
        }
    }
}