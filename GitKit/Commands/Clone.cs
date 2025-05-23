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