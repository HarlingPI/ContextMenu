using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitKit
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2026/2/4 10:37:50
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public struct ProjectInfo
    {
        public string Path;
        public string Branch;

        public bool IsEmpty => string.IsNullOrEmpty(Path) || string.IsNullOrEmpty(Branch);
    }
}