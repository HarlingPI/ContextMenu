using GitKit;
using GitKit.Commands;
using PIToolKit.Public;
using PIToolKit.Public.Utils;
using System.IO;
using System.Text.RegularExpressions;

namespace TestProject
{
    [TestClass]
    public sealed class Test
    {
        //[TestMethod]
        //public void GitLibSearchTest()
        //{
        //    var folder = "D:/Projects/";
        //    var projects = new List<string>();
        //    var time = PublicUtility.ReckonTime(() =>
        //    {
        //        GitLib.SearchSubProjects(folder, projects);
        //    });
        //    Console.WriteLine($"检索耗时：{time}ms,共{projects.Count}个项目");
        //    for (int i = 0; i < projects.Count; i++)
        //    {
        //        Console.WriteLine(projects[i]);
        //    }
        //}

        [TestMethod]
        public void FileUtilsTest()
        {
            var folder = "D:/Projects/";
            var projects = new List<string>();
            var time = PublicUtility.ReckonTime(() =>
            {
                projects.AddRange(GitLib.SearchGitProjects(folder));
            });
            Console.WriteLine($"检索耗时：{time}ms,共{projects.Count}个项目");
            for (int i = 0; i < projects.Count; i++)
            {
                Console.WriteLine(projects[i]);
            }
        }
    }
}
