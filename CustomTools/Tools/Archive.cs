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
            foreach (var file in FileUtils.SearchFiles(path))
            {
                Console.WriteLine(file);
            }
            Console.WriteLine("█▉▊▋▌▍▎▏");
            Console.WriteLine("⣷ ⣯ ⣟ ⡿ ⢿ ⣻ ⣽ ⣾");
            Console.WriteLine("◐ ◓ ◑ ◒ ◐ ◓");
        }
    }
}