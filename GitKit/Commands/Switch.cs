using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/27 12:06:13
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Switch : Command
    {
        public override string Description => "切换分支命令";

        public override string Formate => "switch b f";

        public override string[] Parametes => new[] 
        {
            "b:目标分支名称",
            "f:用于指定switch命令要应用于哪些项目    \tf:[s:e]|f:a,b,c...|f:[s~e],a,b,c..."
        };
        public Switch(string workingFolder) : base(workingFolder)
        {
        }
        public override void Excute(string[] projects, uint retry, params string[] args)
        {
            var cmd = $"switch {string.Join(' ', args)}";
            for (int i = 0; i < projects.Length; i++)
            {
                GitLib.ExcuteCommand(projects[i], cmd, retry);
            }
        }
    }
}