using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/23 14:01:02
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Reset : Command
    {
        public override string Description => "Git中的reset命令,默认参数为--hard HEAD";

        public override string Formate => "";

        public override string[] Parametes => null;
        public Reset(string workingFolder) : base(workingFolder)
        {
        }
        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var cmd = "";
            if (args.IsNullOrEmpty())
            {
                cmd = $"reset --hard HEAD";
            }
            else
            {
                cmd = $"reset {string.Join(' ', args)}";
            }
            for (int i = 0; i < projects.Length; i++)
            {
                GitLib.ExcuteCommand(projects[i], cmd, retry);
            }
        }
    }
}