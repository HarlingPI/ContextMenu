using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/27 18:30:46
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class OD : Command
    {
        public override string Description => "调用资源管理器打开指定路径";

        public override string Formate => "od p|f";

        public override string[] Parametes => new[]
        {
            "p:目标文件夹",
            "f:已列出的目录索引",
        };
        private static Regex idxexp = new Regex(@"^\d+$", RegexOptions.Compiled);
        public OD(string workingFolder) : base(workingFolder)
        {
        }
        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var folder = "";
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                var march = idxexp.Match(arg);

                if (march.Success)
                {
                    var index = int.Parse(march.Value);
                    folder = projects[index];
                    args = args.Where(a => !idxexp.IsMatch(a)).ToArray();
                    break;
                }
            }

            if (string.IsNullOrEmpty(folder) && args != null && args.Length > 0)
            {
                folder = args[0];
                folder = folder.Replace("\"", "");
            }

            if (string.IsNullOrEmpty(folder))
            {
                folder = WorkingFolder;
            }

            if (Directory.Exists(folder) || File.Exists(folder))
            {
                try
                {
                    Process.Start("explorer.exe", $"\"{folder}\"");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"无法打开文件夹：{ex.Message}");
                }
            }
            else Console.WriteLine("错误：文件夹/文件夹不存在！");
        }
    }
}