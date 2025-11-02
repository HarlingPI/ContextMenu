using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitKit
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/6/20 22:36:32
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public struct Analyzer
    {
        public string[] FilteredProjects { get; set; }
        public uint Retry { get; set; }
        public char PostCmd { get; set; }
        public string[] Words { get; set; }
        public Analyzer(string typein, string[] projects)
        {
            FilteredProjects = GetFilteredProjects(ref typein, projects);
            Retry = GetRetryCount(ref typein);
            PostCmd = GetPostCommand(ref typein);
            Words = SplitCommand(typein.Trim());
        }

        private static Regex fexp = new Regex(@"f:(?:\d+|\[\d+~\d+\])(?:,(?:\d+|\[\d+~\d+\]))*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static string[] GetFilteredProjects(ref string typein, string[] projects)
        {
            var match = fexp.Match(typein);
            if (match.Success)
            {
                typein = fexp.Replace(typein, "");
                var parts = match.Value[2..].Split(',');
                var temp = new List<string>();
                for (int i = 0; i < parts.Length; i++)
                {
                    var item = parts[i];
                    if (item.StartsWith('[') && item.EndsWith(']'))
                    {
                        //范围选择
                        var range = item[1..^1].Split('~');
                        if (range.Length == 2 && int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end))
                        {
                            for (int j = start; j <= end && j < projects.Length; j++)
                            {
                                temp.Add(projects[j]);
                            }
                        }
                    }
                    else
                    {
                        //单个选择
                        var num = int.Parse(item);
                        if (num >= 0 && num < projects.Length)
                        {
                            temp.Add(projects[num]);
                        }
                    }
                }

                return temp.ToArray();
            }
            return projects;
        }
        private static Regex retryexp = new Regex(@"re\:\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex uintnexp = new Regex(@"\d+", RegexOptions.Compiled);
        private static uint GetRetryCount(ref string str)
        {
            var match = retryexp.Match(str);
            if (match.Success)
            {
                str = retryexp.Replace(str, "");
                match = uintnexp.Match(match.Value);
                return uint.Parse(match.Value);
            }
            else return uint.MaxValue;
        }

        private static Regex finexp = new Regex(@"fin\:\w", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static char GetPostCommand(ref string str)
        {
            var match = finexp.Match(str);
            if (match.Success)
            {
                str = finexp.Replace(str, "");
                return match.Value[^1];
            }
            else return ' ';
        }
        /// <summary>
        /// 命令分割正则表达式，空格分割，""中的空格不分割
        /// </summary>
        private static Regex splitexp = new Regex(@"(\""[^\""]*\"")|(\S+)", RegexOptions.Compiled);
        private static string[] SplitCommand(string command)
        {
            return splitexp
                .Split(command)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .ToArray();
        }
    }
}