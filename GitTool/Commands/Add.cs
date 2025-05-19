using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitTool.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/19 19:15:36
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Add : Command
    {
        public Add(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, params string[] args)
        {
            for (int i = 0; i < projects.Length; i++)
            {
                GitLib.ExcuteCommand(projects[i], new[] { "add ." });
            }
        }
    }
}