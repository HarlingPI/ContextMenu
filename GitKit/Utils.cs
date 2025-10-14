using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace GitKit
{
    /// <summary>
    /// 作者:   Harling
    /// 时间:   2025/7/3 19:32:08
    /// 备注:   此文件通过PIToolKit模板创建
    /// </summary>
    /// <remarks></remarks>
    public static class Utils
    {
        public static bool IsNullOrEmpty(this IEnumerable collection)
        {
            if (collection == null) return true;

            // 优化：检查是否为 ICollection 类型，避免不必要的枚举
            if (collection is ICollection col)
            {
                return col.Count == 0;
            }
            // 如果不是 ICollection 类型，使用 MoveNext() 避免额外的枚举
            return !collection.GetEnumerator().MoveNext();
        }
        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}