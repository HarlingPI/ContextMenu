using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitTool.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/18 10:31:21
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Push : Command
    {
        public Push(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, params string[] args)
        {
            var addcmd = "add .";
            var commitcmd = "commit -m \"{0}\"";
            var pushcmd = "push";

            var option = "";
            if (args != null && args.Length > 0)
            {
                option = args[0];
            }

            if (string.IsNullOrWhiteSpace(option))
            {
                option = "Commit by scripts";
            }
            commitcmd = string.Format(commitcmd, option);
            string[] commands = new[] { addcmd, commitcmd, pushcmd };

            for (int i = 0; i < projects.Length; i++)
            {
                GitLib.ExcuteCommand(projects[i], commands);
            }
        }
    }
}