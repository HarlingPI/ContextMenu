using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/19 19:17:55
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Commit : Command
    {
        public override string Description => "Git中commit命令的简略版";

        public override string Formate => "commit m";

        public override string[] Parametes => new[]
        {
            "m:本次提交的日志记录"
        };
        public Commit(string workingFolder) : base(workingFolder)
        {
        }
        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var option = args.Where(a => a.StartsWith("\"") && a.EndsWith("\"")).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(option))
            {
                option = "\"Commit by scripts\"";
            }

            for (int i = 0; i < projects.Length; i++)
            {
                GitLib.ExcuteCommand(projects[i], $"commit -m {option}", retry);
            }
        }
    }
}