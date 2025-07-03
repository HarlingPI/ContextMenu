using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/18 11:17:58
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Clone : Command
    {
        public override string Description => "Git中的克隆命令,默认携带recursive参数";
        public override string Formate => "clone u p";
        public override string[] Parametes => new[]
        {
            "u:远程仓库地址",
            "p:克隆到的本地路径"
        };
        public Clone(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            string folder = GitLib.GetProjectNameFromUrl(args[0]);
            string path = Path.Combine(WorkingFolder, folder);
            string cmd = $"clone --recursive {args[0]} {path}";
            GitLib.ExcuteCommand(path, cmd, retry, false);
        }
    }
}