// <copyright file="OutputProcessor.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using EnvDTE;
    using EnvDTE80;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.TextTemplating;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using VSLangProj;
    using Constants = EnvDTE.Constants;

    /// <summary>
    /// This directive processor is a part of the T4 Toolbox infrastructure. Don't
    /// use it in your templates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is responsible for saving generated output files and adding them
    /// to the Visual Studio solution to which the .tt file belonogs. Originally,
    /// this code was defined in the TransformationContext class and executed in the
    /// templating <see cref="AppDomain"/>, as part of <see cref="TextTransformation"/>.
    /// However, in order to support project types such as VSTS Database projects, which 
    /// are implemented in .NET and cannot be accessed accross <see cref="AppDomain"/>
    /// boundries, this code had to be moved here.
    /// </para>
    /// <para>
    /// T4Toolbox.tt uses a custom directive called dte to make the T4 <see cref="Microsoft.VisualStudio.TextTemplating.Engine"/>
    /// load this directive processor for each transformation. The <see cref="DirectiveProcessor.Initialize"/>
    /// method captures the <see cref="ITextTemplatingEngineHost"/> object that
    /// hosts the transformation and also provides access to <see cref="DTE"/>, the
    /// Visual Studio automation model. Later, when OnTransformationEnded method of
    /// the TransformationContext executes, it serializes the <see cref="OutputManager"/>
    /// from the templating <see cref="AppDomain"/> and invokes its <see cref="OutputManager.UpdateFiles"/>
    /// method in the main <see cref="AppDomain"/> of Visual Studio, which in its
    /// turn calls the <see cref="UpdateFiles"/> method of <see cref="OutputProcessor"/>.
    /// </para>
    /// <para>
    /// The <see cref="UpdateFiles"/> is responsible for managing the generated
    /// files as part of the Visual Studio solution. It access the <see cref="ITextTemplatingEngineHost"/>
    /// and <see cref="DTE"/> to do so. This code resides here, in <see cref="OutputProcessor"/>,
    /// as opposed to the <see cref="OutputManager"/> because <see cref="OutputManager"/>
    /// lives primarily in the templating <see cref="AppDomain"/>. Mixing methods that
    /// execute in different domains inside of the <see cref="OutputManager"/> class
    /// was too confusing.
    /// </para>
    /// </remarks>
    //No longer a Directive Processor, but simply a static class.
    public static class OutputProcessor //: DirectiveProcessor
    {
        /// <summary>
        /// Writes generated content to output files and deletes the old files that 
        /// were not regenerated.
        /// </summary>
        /// <param name="outputFiles">
        /// A collection of <see cref="OutputFile"/> objects.
        /// </param>
        /// <remarks>
        /// <see cref="OutputManager"/> calls this method to update generated output 
        /// files and delete old files that were not regenerated. This method accesses
        /// Visual Studio automation model (EnvDTE) to automatically add generated 
        /// files to the solution and source control.
        /// </remarks>
        internal static void UpdateFiles(ICollection<OutputFile> outputFiles)
        {
            //Protect against unnecessary processing
            if (outputFiles.Count > 0)
            {
                string previousDirectory = Environment.CurrentDirectory;

                var templatefile = Host.TemplateFile;

                Environment.CurrentDirectory = Path.GetDirectoryName(Host.TemplateFile);
                try
                {
                    Solution solution = GetDte().Solution;
                    IEnumerable<Project> projects = GetAllProjects(solution);
                    ProjectItem template = solution.FindProjectItem(Host.TemplateFile);

                    Validate(outputFiles, projects);
                    CreateLogIfNecessary(outputFiles, template);
                    DeleteOldOutputs(outputFiles, solution, template);
                    UpdateOutputFiles(outputFiles, solution, projects, template);
                }
                finally
                {
                    Environment.CurrentDirectory = previousDirectory;
                }
            }
        }

        private static ITextTemplatingEngineHost host;
        public static ITextTemplatingEngineHost Host
        {
            get { return host; }
            set { host = value; }
        }

        /// <summary>
        /// Saves content accumulated by this transformation to the output files.
        /// </summary>
        /// <param name="outputFiles">
        /// <see cref="OutputFile"/>s that need to be added to the <paramref name="solution"/>.
        /// </param>
        /// <param name="solution">
        /// Current Visual Studio <see cref="Solution"/>.
        /// </param>
        /// <param name="projects">
        /// All <see cref="Project"/>s in the current <paramref name="solution"/>.
        /// </param>
        /// <param name="template">
        /// A <see cref="ProjectItem"/> that represents T4 template being transformed.
        /// </param>
        /// <remarks>
        /// Note that this method currently cannot distinguish between files that are
        /// already in a Database project and files that are simply displayed with 
        /// "Show All Files" option. Database project model makes these items appear 
        /// as if they were included in the project.
        /// </remarks>
        private static void UpdateOutputFiles(IEnumerable<OutputFile> outputFiles, Solution solution, IEnumerable<Project> projects, ProjectItem template)
        {
            foreach (OutputFile output in outputFiles)
            {
                UpdateOutputFile(output); // Save the output file before we can add it to the solution

                ProjectItem outputItem = solution.FindProjectItem(output.File);
                ProjectItems collection = FindProjectItemCollection(output, projects, template);

                if (outputItem == null)
                {
                    // If output file has not been added to the solution
                    outputItem = collection.AddFromFile(output.File);
                }
                else if (!Same(outputItem.Collection, collection))
                {
                    // If the output file moved from one collection to another                    
                    string backupFile = output.File + ".bak";
                    File.Move(output.File, backupFile); // Prevent unnecessary source control operations
                    outputItem.Delete(); // Remove doesn't work on "DependentUpon" items
                    File.Move(backupFile, output.File);

                    outputItem = collection.AddFromFile(output.File);
                }

                SetProjectItemProperties(outputItem, output);
                SetProjectItemBuildProperties(outputItem, output);
                AddProjectItemReferences(outputItem, output);
            }
        }

        /// <summary>
        /// Determines whether two project items collections are the same.
        /// </summary>
        /// <param name="collection1">First <see cref="ProjectItems"/> collection.</param>
        /// <param name="collection2">Second <see cref="ProjectItems"/> collection.</param>
        /// <returns>True, if the two collections are the same.</returns>
        /// <remarks>
        /// This method is necessary for MPF-based project implementations, such as database projects, which can return different 
        /// ProjectItems instances ultimately pointing to the same folder.
        /// </remarks>
        private static bool Same(ProjectItems collection1, ProjectItems collection2)
        {
            if (collection1 == collection2)
            {
                return true;
            }

            ProjectItem parentItem1 = collection1.Parent as ProjectItem;
            ProjectItem parentItem2 = collection2.Parent as ProjectItem;
            if (parentItem1 != null && parentItem2 != null)
            {
                if (string.Compare(parentItem1.Name, parentItem2.Name, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    return false;
                }

                return Same(parentItem1.Collection, parentItem2.Collection);
            }

            Project parentProject1 = collection1.Parent as Project;
            Project parentProject2 = collection2.Parent as Project;
            if (parentProject1 != null && parentProject2 != null)
            {
                return string.Compare(parentProject1.FullName, parentProject2.FullName, StringComparison.OrdinalIgnoreCase) == 0;
            }

            return false;
        }

        /// <summary>
        /// Adds a folder to a specified <paramref name="collection"/> of project items.
        /// </summary>
        /// <param name="collection">
        /// A <see cref="ProjectItems"/> collection that belongs to a <see cref="Project"/> or 
        /// <see cref="ProjectItem"/> of type <see cref="Constants.vsProjectItemKindPhysicalFolder"/>.
        /// </param>
        /// <param name="folderName">
        /// Name of the folder to be added.
        /// </param>
        /// <param name="basePath">
        /// Absolute path to the directory where the folder is located.
        /// </param>
        /// <returns>
        /// A <see cref="ProjectItem"/> that represents new folder added to the <see cref="Project"/>.
        /// </returns>
        /// <remarks>
        /// If the specified folder doesn't exist in the solution and the file system, 
        /// a new folder will be created in both. However, if the specified folder 
        /// already exists in the file system, it will be added to the solution instead. 
        /// Unfortunately, an existing folder can only be added to the solution with 
        /// all of sub-folders and files in it. Thus, if a single output file is 
        /// generated in an existing folders not in the solution, the target folder will 
        /// be added to the solution with all files in it, generated or not. The 
        /// only way to avoid this would be to immediately remove all child items 
        /// from a newly added existing folder. However, this could lead to having 
        /// orphaned files that were added to source control and later excluded from 
        /// the project. We may need to revisit this code and access <see cref="SourceControl"/> 
        /// automation model to remove the child items from source control too.
        /// </remarks>
        private static ProjectItem AddFolder(ProjectItems collection, string folderName, string basePath)
        {
            // Does the folder already exist in the solution?
            ProjectItem folder = collection.Cast<ProjectItem>().FirstOrDefault(
                p => string.Compare(p.Name, folderName, StringComparison.OrdinalIgnoreCase) == 0);
            if (folder != null)
            {
                return folder;
            }

            try
            {
                // Try adding folder to the project.
                // Note that this will work for existing folder in a Database project but not in C#.
                return collection.AddFolder(folderName, Constants.vsProjectItemKindPhysicalFolder);
            }
            catch (COMException)
            {
                // If folder already exists on disk and the previous attempt to add failed
                string folderPath = Path.Combine(basePath, folderName);
                if (Directory.Exists(folderPath))
                {
                    // Try adding it from disk
                    // Note that this will work in a C# but is not implemented in Database projects.
                    return collection.AddFromDirectory(folderPath);
                }

                throw;
            }
        }

        /// <summary>
        /// Finds project item collection for the output file in Visual Studio solution.
        /// </summary>
        /// <param name="output">
        /// An <see cref="OutputFile"/> that needs to be added to the solution.
        /// </param>
        /// <param name="projects">
        /// All <see cref="Project"/>s in the current <see cref="Solution"/>.
        /// </param>
        /// <param name="template">
        /// A <see cref="ProjectItem"/> that represents T4 template being transformed.
        /// </param>
        /// <returns>A <see cref="ProjectItems"/> collection where the generated file should be added.</returns>
        private static ProjectItems FindProjectItemCollection(OutputFile output, IEnumerable<Project> projects, ProjectItem template)
        {
            ProjectItems collection; // collection to which output file needs to be added
            string relativePath;     // path from the collection to the file
            string basePath;         // absolute path to the directory to which an item is being added

            if (!string.IsNullOrEmpty(output.Project))
            {
                // If output file needs to be added to another project
                Project project = projects.First(p => OutputInfo.SamePath(GetPath(p), output.Project));
                collection = project.ProjectItems;
                relativePath = GetRelativePath(GetPath(project), output.File);
                basePath = Path.GetDirectoryName(GetPath(project));
            }
            else if (output.PreserveExistingFile || !OutputInfo.SamePath(Path.GetDirectoryName(output.File), Environment.CurrentDirectory))
            {
                // If output file needs to be added to another folder of the current project
                collection = template.ContainingProject.ProjectItems;
                relativePath = GetRelativePath(GetPath(template.ContainingProject), output.File);
                basePath = Path.GetDirectoryName(GetPath(template.ContainingProject));
            }
            else
            {
                // Add the output file to the list of children of the template file
                collection = template.ProjectItems;
                relativePath = GetRelativePath(template.get_FileNames(1), output.File);
                basePath = Path.GetDirectoryName(template.get_FileNames(1));
            }

            // make sure that all folders in the file path exist in the project.
            if (relativePath.StartsWith("." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                // Remove leading .\ from the path
                relativePath = relativePath.Substring(relativePath.IndexOf(Path.DirectorySeparatorChar) + 1);

                while (relativePath.Contains(Path.DirectorySeparatorChar))
                {
                    string folderName = relativePath.Substring(0, relativePath.IndexOf(Path.DirectorySeparatorChar));
                    ProjectItem folder = AddFolder(collection, folderName, basePath);

                    collection = folder.ProjectItems;
                    relativePath = relativePath.Substring(folderName.Length + 1);
                    basePath = Path.Combine(basePath, folderName);
                }
            }

            return collection;
        }

        /// <summary>
        /// Returns a list of build actions available for the specified <paramref name="projectItem"/>.
        /// </summary>
        /// <param name="projectItem">
        /// A <see cref="ProjectItem"/> object.
        /// </param>
        /// <returns>
        /// A list where each item contains a name of available build action.
        /// </returns>
        private static ICollection<string> GetAvailableBuildActions(ProjectItem projectItem)
        {
            Microsoft.Build.Evaluation.Project buildProject = new Microsoft.Build.Evaluation.Project(projectItem.ContainingProject.FullName);
            List<string> buildActions = new List<string> { "None", "Compile", "Content", "EmbeddedResource" };
            var items = buildProject.Items.Where(ip => ip.ItemType == "AvailableItemName");
            foreach (var item in items)
            {
                buildActions.Add(item.EvaluatedInclude);
            }

            //#pragma warning disable 618
            // Microsoft.Build.Engine.Project is obsolete in .NET 4.0. 
            // Code in this method will need to be rewritten when we drop support for DEV9.
            //Microsoft.Build.BuildEngine.Project buildProject = new Microsoft.Build.BuildEngine.Project();
            //#pragma warning restore 618
            //buildProject.LoadXml(File.ReadAllText(GetPath(projectItem.ContainingProject)));
            //List<string> buildActions = new List<string> { "None", "Compile", "Content", "EmbeddedResource" };
            //foreach (Microsoft.Build.BuildEngine.BuildItemGroup itemGroup in buildProject.ItemGroups)
            //{
            //foreach (MSBuild.BuildItem buildItem in itemGroup)
            //{
            //    if (buildItem.Name == "AvailableItemName")
            //    {
            //        buildActions.Add(buildItem.Include);
            //    }
            //}
            //}
            return buildActions;
        }

        /// <summary>
        /// Sets the known properties for the <see cref="ProjectItem"/> to be added to solution.
        /// </summary>
        /// <param name="projectItem">
        /// A <see cref="ProjectItem"/> that represents the generated item in the solution.
        /// </param>        
        /// <param name="output">
        /// An <see cref="OutputFile"/> that holds metadata about the <see cref="ProjectItem"/> to be added to the solution.
        /// </param>
        private static void SetProjectItemProperties(ProjectItem projectItem, OutputFile output)
        {
            // Set "Build Action" property
            if (!string.IsNullOrEmpty(output.BuildAction))
            {
                ICollection<string> buildActions = GetAvailableBuildActions(projectItem);
                if (!buildActions.Contains(output.BuildAction))
                {

                    throw new TransformationException(
                        string.Format(CultureInfo.CurrentCulture, "Build Action {0} is not supported for {1}", output.BuildAction, projectItem.Name));
                }

                SetPropertyValue(projectItem, "ItemType", output.BuildAction);
            }

            // Set "Copy to Output Directory" property
            if (output.CopyToOutputDirectory != default(CopyToOutputDirectory))
            {
                SetPropertyValue(projectItem, "CopyToOutputDirectory", (int)output.CopyToOutputDirectory);
            }

            // Set "Custom Tool" property
            if (!string.IsNullOrEmpty(output.CustomTool))
            {
                SetPropertyValue(projectItem, "CustomTool", output.CustomTool);
            }

            // Set "Custom Tool Namespace" property
            if (!string.IsNullOrEmpty(output.CustomToolNamespace))
            {
                SetPropertyValue(projectItem, "CustomToolNamespace", output.CustomToolNamespace);
            }
        }

        /// <summary>
        /// Sets the known properties for the <see cref="ProjectItem"/> added to solution.
        /// </summary>
        /// <param name="projectItem">
        /// A <see cref="ProjectItem"/> that represents the generated item in the solution.
        /// </param>        
        /// <param name="output">
        /// An <see cref="OutputFile"/> that holds metadata about the <see cref="ProjectItem"/> to be added to the solution.
        /// </param>
        private static void SetProjectItemBuildProperties(ProjectItem projectItem, OutputFile output)
        {
            if (output.BuildProperties.Count > 0)
            {
                // Get access to the build property storage service
                IServiceProvider serviceProvider = (IServiceProvider)Host;
                IVsSolution solution = (IVsSolution)serviceProvider.GetService(typeof(SVsSolution));
                IVsHierarchy hierarchy;
                ErrorHandler.ThrowOnFailure(solution.GetProjectOfUniqueName(projectItem.ContainingProject.UniqueName, out hierarchy));
                IVsBuildPropertyStorage buildPropertyStorage = hierarchy as IVsBuildPropertyStorage;
                if (buildPropertyStorage == null)
                {
                    throw new TransformationException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            "Project {0} does not support build properties required by {1}",
                            projectItem.ContainingProject.Name,
                            projectItem.Name));
                }

                // Find the target project item in the property storage
                uint projectItemId;
                ErrorHandler.ThrowOnFailure(hierarchy.ParseCanonicalName(projectItem.get_FileNames(1), out projectItemId));

                // Set build projerties for the target project item
                foreach (KeyValuePair<string, string> buildProperty in output.BuildProperties)
                {
                    ErrorHandler.ThrowOnFailure(buildPropertyStorage.SetItemAttribute(projectItemId, buildProperty.Key, buildProperty.Value));
                }
            }
        }

        /// <summary>
        /// Adds assembly references required by the project item to the target project.
        /// </summary>
        /// <param name="projectItem">
        /// A <see cref="ProjectItem"/> that represents the generated item in the solution.
        /// </param>        
        /// <param name="output">
        /// An <see cref="OutputFile"/> that holds metadata about the <see cref="ProjectItem"/> to be added to the solution.
        /// </param>
        private static void AddProjectItemReferences(ProjectItem projectItem, OutputFile output)
        {
            if (output.References.Count > 0)
            {
                VSProject project = projectItem.ContainingProject.Object as VSProject;
                if (project == null)
                {
                    throw new TransformationException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            "Project {0} does not support assembly references required by {1}",
                            projectItem.ContainingProject.Name,
                            projectItem.Name));
                }

                foreach (string reference in output.References)
                {
                    project.References.Add(reference);
                }
            }
        }

        /// <summary>
        /// Sets property value for the <paramref name="projectItem"/>.
        /// </summary>
        /// <param name="projectItem">Target <see cref="ProjectItem"/> object.</param>
        /// <param name="propertyName">Target property name.</param>
        /// <param name="value">Property value.</param>
        /// <exception cref="TransformationException">
        /// When the <paramref name="projectItem"/> doesn't have a property with the specified <paramref name="propertyName"/>.
        /// </exception>
        private static void SetPropertyValue(ProjectItem projectItem, string propertyName, object value)
        {
            Property property = projectItem.Properties.Item(propertyName);
            if (property == null)
            {
                throw new TransformationException(
                    string.Format(CultureInfo.CurrentCulture, "Property {0} is not supported for {1}", propertyName, projectItem.Name));
            }

            property.Value = value;
        }

        /// <summary>
        /// Deletes output files that were not generated by the current session.
        /// </summary>
        /// <param name="outputFiles">
        /// A read-only collection of <see cref="OutputFile"/> objects.
        /// </param>
        /// <param name="solution">
        /// Current Visual Studio <see cref="Solution"/>.
        /// </param>
        /// <param name="template">
        /// A <see cref="ProjectItem"/> that represents T4 template being transformed.
        /// </param>
        private static void DeleteOldOutputs(IEnumerable<OutputFile> outputFiles, Solution solution, ProjectItem template)
        {
            // If previous transformation produced a log of generated ouptut files
            string logFile = GetLogFileName();
            if (File.Exists(logFile))
            {
                // Delete all files recorded in the log that were not regenerated
                foreach (string line in File.ReadAllLines(logFile))
                {
                    string relativePath = line.Trim();

                    // Skip blank lines
                    if (relativePath.Length == 0)
                    {
                        continue;
                    }

                    // Skip comments
                    if (relativePath.StartsWith("//", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string absolutePath = Path.GetFullPath(relativePath);

                    // Skip the file if it was regenerated during current transformation
                    if (outputFiles.Any(output => OutputInfo.SamePath(output.File, absolutePath)))
                    {
                        continue;
                    }

                    // The file wasn't regenerated, delete it from the solution, source control and file storage
                    ProjectItem projectItem = solution.FindProjectItem(absolutePath);
                    if (projectItem != null)
                    {
                        DeleteProjectItem(projectItem);
                    }
                }
            }

            // Also delete all project items nested under template if they weren't regenerated
            string templateFileName = Path.GetFileNameWithoutExtension(Host.TemplateFile);
            foreach (ProjectItem childProjectItem in template.ProjectItems)
            {
                // Skip the file if it has the same name as the template file. This will prevent constant
                // deletion and adding of the main output file to the project, which is slow and may require 
                // the user to check the file out unnecessarily.
                if (templateFileName == Path.GetFileNameWithoutExtension(childProjectItem.Name))
                {
                    continue;
                }

                // If the file wan't regenerated, delete it from the the solution, source control and file storage
                if (!outputFiles.Any(o => OutputInfo.SamePath(o.File, childProjectItem.get_FileNames(1))))
                {
                    childProjectItem.Delete();
                }
            }
        }

        /// <summary>
        /// Creates a log file if any files were generated in a different folder or project.
        /// </summary>
        /// <param name="outputFiles">
        /// A collection of <see cref="OutputFile"/> objects.
        /// </param>
        /// <param name="template">
        /// A <see cref="ProjectItem"/> that represents T4 template being transformed.
        /// </param>
        /// <remarks>
        /// A log file is created as an <see cref="OutputFile"/> object and added to 
        /// the collection. The <see cref="UpdateOutputFiles"/> method takes care of 
        /// saving it.
        /// </remarks>
        private static void CreateLogIfNecessary(ICollection<OutputFile> outputFiles, ProjectItem template)
        {
            string templateProject = GetPath(template.ContainingProject);
            string templateDirectory = Environment.CurrentDirectory;

            //Only create a log file when the directories or the projects are different.
            if (outputFiles.Any(output =>
                                !OutputInfo.SamePath(Path.GetDirectoryName(output.File), templateDirectory)
                                || (!string.IsNullOrEmpty(output.Project) && !OutputInfo.SamePath(output.Project, templateProject))))
            {
                OutputFile log = new OutputFile();
                log.File = GetLogFileName();
                log.Project = string.Empty;
                outputFiles.Add(log);

                log.Content.AppendLine("// <autogenerated>");
                log.Content.AppendLine("//   This file contains the list of files generated by " + Path.GetFileName(Host.TemplateFile) + ".");
                log.Content.AppendLine("//   It is used by T4 Toolbox (http://www.codeplex.com/t4toolbox) to automatically delete");
                log.Content.AppendLine("//   generated files that are no longer necessary. Do not modify this file manually. Manual");
                log.Content.AppendLine("//   changes may cause orphaned files and will be lost next time the code is regenerated.");
                log.Content.AppendLine("// </autogenerated>");
                foreach (OutputFile output in outputFiles)
                {
                    // Don't log the generated files that will be preserved 
                    if (!output.PreserveExistingFile)
                    {
                        log.Content.AppendLine(GetRelativePath(Host.TemplateFile, output.File));
                    }
                }
            }
        }

        /// <summary>
        /// Deletes the specified <paramref name="item"/> and its parent folders if they are empty.
        /// </summary>
        /// <param name="item">A Visual Studio <see cref="ProjectItem"/>.</param>
        /// <remarks>
        /// This method correctly deletes empty parent folders in C# and probably 
        /// Visual Basic projects which are implemented in C++ as pure COM objects. 
        /// However, for Database and probably WiX projects, which are implemented 
        /// as .NET COM objects, the parent collection indicates item count = 1 
        /// even after its only child item is deleted. So, for new project types,
        /// this method doesn't delete empty parent folders. However, this is probably
        /// desirable for Database projects that create a predefined, empty folder 
        /// structure for each schema. We may need to solve this problem in the 
        /// future by recording which folders were actually created by the code 
        /// generator in the log file and deleting the empty parent folders when 
        /// the previously generated folders become empty.
        /// </remarks>
        private static void DeleteProjectItem(ProjectItem item)
        {
            ProjectItems parentCollection = item.Collection;

            item.Delete();

            if (parentCollection.Count == 0)
            {
                ProjectItem parent = parentCollection.Parent as ProjectItem;
                if (parent != null && parent.Kind == Constants.vsProjectItemKindPhysicalFolder)
                {
                    DeleteProjectItem(parent);
                }
            }
        }

        /// <summary>
        /// Creates a new output file or updates it's contents if it already exists.
        /// </summary>
        /// <param name="output">An <see cref="OutputFile"/> object.</param>
        private static void UpdateOutputFile(OutputFile output)
        {
            // Don't do anything unless the output file has changed and needs to be overwritten
            if (File.Exists(output.File))
            {
                if (output.PreserveExistingFile || output.Content.ToString() == File.ReadAllText(output.File, output.Encoding))
                {
                    return;
                }
            }

            // Check out the file if it is under source control
            SourceControl sourceControl = GetDte().SourceControl;
            if (sourceControl.IsItemUnderSCC(output.File) && !sourceControl.IsItemCheckedOut(output.File))
            {
                sourceControl.CheckOutItem(output.File);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output.File));
            File.WriteAllText(output.File, output.Content.ToString(), output.Encoding);
        }

        /// <summary>
        /// Performs validation tasks that require accessing Visual Studio automation model.
        /// </summary>
        /// <param name="outputFiles">
        /// <see cref="OutputFile"/>s that need to be added to the solution.
        /// </param>
        /// <param name="projects">
        /// All <see cref="Project"/>s in the current solution.
        /// </param>
        /// <remarks>
        /// Most of the output validation is done on the fly, by the <see cref="OutputManager.Append"/>
        /// method. This method performs the remaining validations that access Visual 
        /// Studio automation model and cannot cross <see cref="bAppDomain"/> boundries.
        /// </remarks>
        private static void Validate(IEnumerable<OutputFile> outputFiles, IEnumerable<Project> projects)
        {
            foreach (OutputFile outputFile in outputFiles)
            {
                if (string.IsNullOrEmpty(outputFile.Project))
                {
                    continue;
                }

                // Make sure that project is included in the solution
                bool projectInSolution = projects.Any(p => OutputInfo.SamePath(GetPath(p), outputFile.Project));
                if (!projectInSolution)
                {
                    throw new TransformationException(
                        string.Format(CultureInfo.CurrentCulture, "Target project {0} does not belong to the solution", outputFile.Project));
                }
            }
        }

        /// <summary>
        /// Retrieves projects from the specified <paramref name="solution"/>.
        /// </summary>
        /// <param name="solution">Visual Studio <see cref="Solution"/> object.</param>
        /// <returns>
        /// Collection of all projects in the <paramref name="solution"/> or one of its folders.
        /// </returns>
        private static IEnumerable<Project> GetAllProjects(Solution solution)
        {
            List<Project> projects = new List<Project>();
            foreach (Project project in solution.Projects)
            {
                AddAllProjects(project, projects);
            }

            return projects;
        }

        /// <summary>
        /// Adds projects, recursively, from the specified <paramref name="solutionItem"/> 
        /// to the collection.
        /// </summary>
        /// <param name="solutionItem">
        /// A <see cref="Project"/> object that can also represent a solution folder.
        /// </param>
        /// <param name="projects">
        /// A collection of all solution projects.
        /// </param>
        private static void AddAllProjects(Project solutionItem, ICollection<Project> projects)
        {
            if (solutionItem.Kind == ProjectKinds.vsProjectKindSolutionFolder)
            {
                foreach (ProjectItem item in solutionItem.ProjectItems)
                {
                    if (item.SubProject != null)
                    {
                        AddAllProjects(item.SubProject, projects);
                    }
                }
            }
            else
            {
                projects.Add(solutionItem);
            }
        }

        /// <summary>
        /// Retrieves top-level Visual Studio automation object.
        /// </summary>
        /// <returns>
        /// A <see cref="DTE"/> object of this Visual Studio instance hosting the transformation.
        /// </returns>
        private static DTE GetDte()
        {
            IServiceProvider serviceProvider = (IServiceProvider)Host;
            return (DTE)serviceProvider.GetService(typeof(DTE));
        }

        /// <summary>
        /// Returns full path to the project file.
        /// </summary>
        /// <param name="project">
        /// Visual Studio <see cref="Project"/> object.
        /// </param>
        /// <returns>
        /// A <see cref="String"/> with full path to the project file or a valid, but 
        /// non-existent path if the <see cref="Project"/> does not support the FullName property.
        /// </returns>
        private static string GetPath(Project project)
        {
            try
            {
                return project.FullName;
            }
            catch (NotImplementedException)
            {
                return @"PATH:\NOT\AVAILABLE";
            }
        }

        /// <summary>
        /// Converts absolute path to a relative path.
        /// </summary>
        /// <param name="fromFile">
        /// Full path to the base file.
        /// </param>
        /// <param name="toFile">
        /// Full path to the target file.
        /// </param>
        /// <returns>
        /// Relative path to the specified file.
        /// </returns>
        private static string GetRelativePath(string fromFile, string toFile)
        {
            StringBuilder relativePath = new StringBuilder(260);
            if (!NativeMethods.PathRelativePathTo(relativePath, fromFile, 0, toFile, 0))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Cannot convert '{0}' to a path relative to the location of '{1}'.",
                        toFile,
                        fromFile));
            }

            return relativePath.ToString();
        }

        /// <summary>
        /// Determines name of the log file.
        /// </summary>
        /// <returns>
        /// Full path and file name of the log file.
        /// </returns>
        private static string GetLogFileName()
        {
            return Host.TemplateFile + ".log";
        }
    }
}
