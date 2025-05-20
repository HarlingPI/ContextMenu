using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/20 22:39:40
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class CD : Command
    {
        public CD(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, params string[] args)
        {
            var folder = "";
            if (args != null && args.Length > 0)
            {
                folder = args[0];
                folder = folder.Replace("\"", "");
            }
            Program.InitProgram(folder);
        }
    }
}