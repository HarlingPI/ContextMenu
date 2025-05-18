using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitTool.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/18 10:31:21
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Push : Command
    {
        public Push(string workingFolder) : base(workingFolder)
        {
        }

        public override bool Excute(params string[] args)
        {
            throw new NotImplementedException();
        }
    }
}