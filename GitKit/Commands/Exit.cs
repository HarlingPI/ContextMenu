using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit.Commands
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/18 21:30:50
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public class Exit : Command
    {
        public Exit(string workingFolder) : base(workingFolder)
        {
        }

        public override void Excute(string[] projects, params string[] args)
        {
            Environment.Exit(0);
        }
    }
}