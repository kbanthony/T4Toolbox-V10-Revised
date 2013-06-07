using EnvDTE;
using EnvDTE80;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace TestT4Debugging
{
    class Program
    {
        static void Main(string[] args)
        {
            //System.Diagnostics.Debugger.Launch();
            //System.Diagnostics.Debugger.Break();
            try
            {
                //Microsoft.Build.BuildEngine.Project buildProject = new Microsoft.Build.BuildEngine.Project();

                //Microsoft.Build.Evaluation.Project buildProject = new Microsoft.Build.Evaluation.Project(@"C:\DB\Dropbox\VS\VS12\T4Templates\TestT4Debugging10R\TestT4Debugging\TestT4Debugging.csproj");
                //List<string> buildActions = new List<string> { "None", "Compile", "Content", "EmbeddedResource" };
                //var items = buildProject.Items.Where(ip => ip.ItemType == "AvailableItemName");
                //foreach (var item in items)
                //{
                //    buildActions.Add(item.EvaluatedInclude);

                //buildProject.LoadXml(@"C:\DB\Dropbox\VS\VS12\T4Templates\TestT4Debugging10R\TestT4Debugging\TestT4Debugging.csproj");
                //var items = buildProject.ItemGroups;

                //var slnp = SolutionProjects.GetActiveIDE();

                //var activeinstances = SolutionProjects.GetInstances();
                //var prgs = SolutionProjects.Projects();
                //var item = activeinstances.FirstOrDefault().ActiveSolutionProjects;// GetEnumerator();
            }
            catch (Exception ex)
            {
            }
        }
    }

    public static class SolutionProjects
    {
        public static DTE2 GetActiveIDE()
        {
            // Get an instance of the currently running Visual Studio IDE.
            var dte2 = (DTE2)System.Runtime.InteropServices.Marshal.GetActiveObject("VisualStudio.DTE.11.0");
            //var dte2 = (DTE2)System.Runtime.InteropServices.Marshal.GetActiveObject("VisualStudio.DTE.10.0");
            return dte2;
        }

        public static IList<Project> Projects()
        {
            Projects projects = GetActiveIDE().Solution.Projects;
            List<Project> list = new List<Project>();
            var item = projects.GetEnumerator();
            while (item.MoveNext())
            {
                var project = item.Current as Project;
                if (project == null)
                {
                    continue;
                }

                if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    list.AddRange(GetSolutionFolderProjects(project));
                }
                else
                {
                    list.Add(project);
                }
            }

            return list;
        }

        private static IEnumerable<Project> GetSolutionFolderProjects(Project solutionFolder)
        {
            List<Project> list = new List<Project>();
            for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++)
            {
                var subProject = solutionFolder.ProjectItems.Item(i).SubProject;
                if (subProject == null)
                {
                    continue;
                }

                // If this is another solution folder, do a recursive call, otherwise add
                if (subProject.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    list.AddRange(GetSolutionFolderProjects(subProject));
                }
                else
                {
                    list.Add(subProject);
                }
            }

            return list;
        }


        public static IEnumerable<DTE> GetInstances()
        {
            IRunningObjectTable rot;
            IEnumMoniker enumMoniker;
            int retVal = GetRunningObjectTable(0, out rot);

            if (retVal == 0)
            {
                rot.EnumRunning(out enumMoniker);

                IntPtr fetched = IntPtr.Zero;
                IMoniker[] moniker = new IMoniker[1];
                while (enumMoniker.Next(1, moniker, fetched) == 0)
                {
                    IBindCtx bindCtx;
                    CreateBindCtx(0, out bindCtx);
                    string displayName;
                    moniker[0].GetDisplayName(bindCtx, null, out displayName);
                    Console.WriteLine("Display Name: {0}", displayName);
                    bool isVisualStudio = displayName.StartsWith("!VisualStudio");
                    if (isVisualStudio)
                    {
                        object dte = null;
                        //var dte = rot.GetObject(moniker) as DTE;
                        var x = rot.GetObject(moniker[0], out dte);
                        yield return dte as DTE;
                    }
                }
            }
        }

        [DllImport("ole32.dll")]
        private static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);
    }
}
