using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitTool.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/19 19:17:55
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Commit : Command
    {
        public Commit(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, params string[] args)
        {
            var option = args.Where(a => a.StartsWith("\"") && a.EndsWith("\"")).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(option))
            {
                option = "Commit by scripts";
            }

            for (int i = 0; i < projects.Length; i++)
            {
                GitLib.ExcuteCommand(projects[i], new[] { $"commit -m {option}" });
            }
        }
    }
}