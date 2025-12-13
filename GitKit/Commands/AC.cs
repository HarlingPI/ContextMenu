using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/12/13 10:57:38
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class AC : Command
    {
        public override string Description => "git中'add'、'commit'命令的结合命令";
        public override string Formate => "ac m f r";
        public override string[] Parametes => new[]
        {
            "m:commit的信息,默认为'Commit by scripts'",
            "f:用于指定acp命令要应用于哪些项目    \tf:[s~e]|f:a,b,c...|f:[s~e],a,b,c...",
            "r:用于指定本命令最大重试次数    \tre:n",
        };
        public AC(string workingFolder) : base(workingFolder)
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
                var project = projects[i];
                var add = "";
                if (WorkingFolder.Contains(project))
                {
                    add = $"add \"{WorkingFolder.Replace(project, ".")}\"";
                }
                else add = $"add .";
                //Add
                GitLib.ExcuteCommand(project, add, retry);
                //commit
                GitLib.ExcuteCommand(project, $"commit -m {option}", retry);
            }
        }
    }
}