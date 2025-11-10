using ConsoleKit;
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
                var files = FileUtils.SearchFiles(path, greed: false).ToArray();
                var folders = FileUtils.SearchFolders(path, int.MaxValue).ToArray();
                return (files, folders);
            });
            Effects.ShowSpinner2Char("Searching", search);
            Console.WriteLine($"已搜索到文件夹:{search.Result.folders.Length},文件:{search.Result.files.Length}");
        }
    }
}