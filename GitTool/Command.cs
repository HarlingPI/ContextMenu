using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitTool
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/5/18 10:17:08
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks>命令基类</remarks>
    public abstract class Command
    {
        /// <summary>
        /// 工作路径
        /// </summary>
        protected string WorkingFolder { get;private set; }
        protected Command(string workingFolder)
        {
            WorkingFolder = workingFolder;
        }
        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="projects"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public abstract void Excute(string[] projects, params string[] args);
    }
}