using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/12/13 9:50:16
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Init : Command
    {
        public override string Description => "Git中的Init命令";
        public override string Formate => "init";
        public override string[] Parametes => new string[0];
        public Init(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            string path = WorkingFolder;
            GitLib.ExcuteCommand(path, "init", retry, false);
        }
    }
}