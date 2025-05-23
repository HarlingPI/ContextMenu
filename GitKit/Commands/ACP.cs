using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/20 21:33:17
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class ACP : Command
    {
        public ACP(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var option = args.Where(a => a.StartsWith("\"") && a.EndsWith("\"")).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(option))
            {
                option = "\"Commit by scripts\"";
            }
            else
            {
                args = args.Where(a => a != option).ToArray();
            }

            for (int i = 0; i < projects.Length; i++)
            {
                //Add
                GitLib.ExcuteCommand(projects[i], "add .", retry);
                //commit
                GitLib.ExcuteCommand(projects[i], $"commit -m {option}", retry);
                //push
                GitLib.ExcuteCommand(projects[i], $"push {string.Join(" ", args)}", retry);
            }
        }
    }
}