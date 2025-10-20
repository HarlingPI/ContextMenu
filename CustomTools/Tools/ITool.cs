using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomTools.Tools
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/10/20 19:35:19
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public interface ITool
    {
        /// <summary>
        /// 开始处理
        /// </summary>
        /// <param name="path"></param>
        public void Process(string path);
    }
}