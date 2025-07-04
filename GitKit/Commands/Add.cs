using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/7/4 9:55:51
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Add : Command
    {
        public override string Description => "git中'add'命令";

        public override string Formate => "add [p] f r";

        public override string[] Parametes => new[]
        {
            "p:要添加的目录，可选，为空时优先提交当前目录下的更改，次级提交项目下的所有改动",
            "f:用于指定acp命令要应用于哪些项目    \tf:[s:e]|f:a,b,c...|f:[s:e],a,b,c...",
            "r:用于指定本命令最大重试次数    \tre:n",
        };
        public Add(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            for (int i = 0; i < projects.Length; i++)
            {
                var project = projects[i];
                var add = "";
                if (WorkingFolder.Contains(project))
                {
                    add = $"add \"{WorkingFolder.Replace(project, ".")}\"";
                }
                else if (args.Length > 0)
                {
                    add = $"add {string.Join(" ", args)}";
                }
                else add = $"add .";
                GitLib.ExcuteCommand(project, add, retry);
            }
        }
    }
}