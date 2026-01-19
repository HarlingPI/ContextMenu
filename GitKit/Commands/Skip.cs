using PIToolKit.Public.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/7/3 19:23:35
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Skip : Command
    {
        public override string Description => "保留远端文件的版本，同时忽略本地修改";

        public override string Formate => "skip [p] [o]";

        public override string[] Parametes => new[]
        {
            " :列出所有被忽略文件",
            "p:文件路径",
            "o:对文件的操作 n:取消忽略,s:忽略文件(可选)",
        };
        public Skip(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var cmd = "";
            if (args.IsNullOrEmpty())
            {
                cmd = "ls-files -v | findstr /B \"S \"";
            }
            else
            {
                var o = args[^1].ToLower();
                //支持相对路径
                args[0] = FileUtils.GetFullPath(args[0]);

                if (o == "s" || args.Length == 1)
                {
                    cmd = $"update-index --skip-worktree {args[0]}";
                }
                else if (o == "n")
                {
                    cmd = $"update-index --no-skip-worktree {args[0]}";
                }
                else return;
            }
            for (int i = 0; i < projects.Length; i++)
            {
                GitLib.ExcuteCommand(projects[i], cmd, retry);
            }
        }
    }
}