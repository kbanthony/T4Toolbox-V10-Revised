// <copyright file="TransformationContext.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using EnvDTE;
    using Microsoft.VisualStudio.TextTemplating;
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Reflection;

    /// <summary>
    /// Provides context information about template transformation environment.
    /// </summary>
    /// <remarks>
    /// This class is static by design. It is declared as abstract in order to allow 
    /// <see cref="TransformationContextProcessor"/> to generate a descendant class 
    /// with <see cref="Transformation"/> property strongly-typed as GeneratedTextTransformation.
    /// </remarks>
    public abstract class TransformationContext
    {
        #region fields

        /// <summary>
        /// Stores the top-level Visual Studio automation object.
        /// </summary>
        private static DTE dte;

        /// <summary>
        /// Stores names of output files and their content until the end of the transformation,
        /// when we can be certain that all generated output has been collected and a meaningful
        /// check can be performed to make sure that files that haven't changed are not checked
        /// out unnecessarily.
        /// </summary>
        private static OutputManager outputManager;

        /// <summary>
        /// Visual Studio <see cref="Project"/> to which the template file belongs.
        /// </summary>
        private static Project project;

        /// <summary>
        /// Visual Studio <see cref="ProjectItem"/> representing the template file.
        /// </summary>
        private static ProjectItem projectItem;

        /// <summary>
        /// Currently running, generated <see cref="TextTransformation"/> object.
        /// </summary>
        private static TextTransformation transformation;

        /// <summary>
        /// Trace listener used for displaying debug messages in Visual Studio output window.
        /// </summary>
        private static OutputWindowTraceListener traceListener;

        #endregion

        /// <summary>
        /// Occurs when template transformation has ended.
        /// </summary>
        public static event EventHandler TransformationEnded;

        /// <summary>
        /// Gets default namespace for generated code.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> based on <see cref="RootNamespace"/> of the project 
        /// and location of the template in it.
        /// </value>
        public static string DefaultNamespace
        {
            get
            {
                List<string> namespaces = new List<string>();
                ProjectItem parent = TransformationContext.ProjectItem.Collection.Parent as ProjectItem;
                while (parent != null)
                {
                    if (parent.Kind != Constants.vsProjectItemKindPhysicalFile)
                    {
                        namespaces.Insert(0, parent.Name.Replace(" ", string.Empty));
                    }

                    parent = parent.Collection.Parent as ProjectItem;
                }

                namespaces.Insert(0, TransformationContext.RootNamespace);
                return string.Join(".", namespaces.ToArray());
            }
        }

        /// <summary>
        /// Gets or sets the top-level Visual Studio automation object.
        /// </summary>
        /// <value>
        /// A <see cref="DTE"/> object.
        /// </value>
        /// <exception cref="TransformationException">
        /// When Visual Studio automation object is not available.
        /// </exception>
        /// <remarks>
        /// <see cref="TransformationContext"/> assumes that it is running inside of
        /// Visual Studio T4 host and will automaticaly find the main automation object.
        /// However, when running inside of the comman line T4 host (TextTransform.exe),
        /// Visual Studio is not available. Code generators that require Visual Studio
        /// automation in the command line host can launch it explicitly and assign this
        /// property to enable normal behavior of the <see cref="TransformationContext"/>.
        /// </remarks>
        //JSR [CLSCompliant(false)]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "DTE", Justification = "Property name matches the type name")]
        public static DTE DTE
        {
            get
            {
                if (TransformationContext.dte == null)
                {
                    IServiceProvider hostServiceProvider = (IServiceProvider)TransformationContext.Host;
                    if (hostServiceProvider == null)
                    {
                        throw new TransformationException("Host property returned unexpected value (null)");
                    }

                    TransformationContext.dte = (DTE)hostServiceProvider.GetService(typeof(DTE));
                    if (TransformationContext.dte == null)
                    {
                        throw new TransformationException("Unable to retrieve DTE");
                    }
                }

                return TransformationContext.dte;
            }

            set
            {
                TransformationContext.dte = value;
            }
        }

        /// <summary>
        /// Gets <see cref="ITextTemplatingEngineHost"/> which is running the 
        /// <see cref="Transformation"/>.
        /// </summary>
        /// <value>
        /// An <see cref="ITextTemplatingEngineHost"/> instance.
        /// </value>
        /// <exception cref="TransformationException">
        /// When <see cref="TransformationContext"/> has not been properly initialized;
        /// or when currently running <see cref="TextTransformation"/> is not host-specific.
        /// </exception>
        public static ITextTemplatingEngineHost Host
        {
            get
            {
                if (HostProperty == null)
                {
                    throw new TransformationException(
                        "Unable to access templating engine host. " +
                        "Please make sure your template includes hostspecific=\"True\" " +
                        "parameter in the <#@ template #> directive.");
                }

                return (ITextTemplatingEngineHost)HostProperty.GetValue(Transformation, null);
            }
        }

        /// <summary>
        /// Gets the Visual Studio <see cref="Project"/> to which the template file belongs.
        /// </summary>
        /// <value>
        /// A <see cref="Project"/> object.
        /// </value>
        //JSR [CLSCompliant(false)]
        public static Project Project
        {
            get
            {
                if (TransformationContext.project == null)
                {
                    TransformationContext.project = TransformationContext.ProjectItem.ContainingProject;
                }

                return TransformationContext.project;
            }
        }

        /// <summary>
        /// Gets the Visual Studio <see cref="ProjectItem"/> representing the template file.
        /// </summary>
        /// <value>
        /// A <see cref="ProjectItem"/> object.
        /// </value>
        //JSR [CLSCompliant(false)]
        public static ProjectItem ProjectItem
        {
            get
            {
                if (projectItem == null)
                {
                    projectItem = FindProjectItem(Host.TemplateFile);
                }

                return projectItem;
            }
        }

        /// <summary>
        /// Gets the default namespace specified in the options of the current project.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> with default namespace of the project the template belongs to.
        /// </value>
        public static string RootNamespace
        {
            get
            {
                foreach (Property property in TransformationContext.Project.Properties)
                {
                    if (property.Name == "RootNamespace")
                    {
                        return (string)property.Value;
                    }
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the currently running, generated <see cref="TextTransformation"/> object.
        /// </summary>
        /// <value>
        /// A <see cref="TextTransformation"/> object.
        /// </value>
        /// <exception cref="TransformationException">
        /// When <see cref="TransformationContext"/> has not been properly initialized.
        /// </exception>
        public static TextTransformation Transformation
        {
            get
            {
                if (transformation == null)
                {
                    throw new TransformationException(
                        "Transformation context was not properly initialized. " +
                        "Please make sure your template uses the following directive: " +
                        "<#@ include file=\"T4Toolbox.tt\" #>.");
                }

                return transformation;
            }
        }

        /// <summary>
        /// Gets <see cref="TextTransformation.Errors"/> collection.
        /// </summary>
        /// <exception cref="TransformationException">
        /// When <see cref="TransformationContext"/> has not been properly initialized.
        /// </exception>
        private static CompilerErrorCollection Errors
        {
            get
            {
                // Use reflection instead of simply accessing TextTransformation.Errors because 
                // it is declared as protected internal in the 9.0 version of the type.
                Type transformationType = Transformation.GetType();
                PropertyInfo errorsProperty = transformationType.GetProperty("Errors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return (CompilerErrorCollection)errorsProperty.GetValue(Transformation, null);
            }
        }

        /// <summary>
        /// Gets <see cref="PropertyInfo"/> object that represents
        /// Host property of the GeneratedTextTransformation.
        /// </summary>
        private static PropertyInfo HostProperty
        {
            get
            {
                Type transformationType = Transformation.GetType();
                return transformationType.GetProperty("Host");
            }
        }

        /// <summary>
        /// Returns <see cref="ProjectItem"/> for the specified file.
        /// </summary>
        /// <param name="fileName">
        /// Name of the file.
        /// </param>
        /// <returns>
        /// Visual Studio <see cref="ProjectItem"/> object.
        /// </returns>
        /// <remarks>
        /// This method is used by templates to access CodeModel for generating
        /// output using C# or Visual Basic source code as a model.
        /// </remarks>
        //JSR [CLSCompliant(false)]
        public static ProjectItem FindProjectItem(string fileName)
        {
            if (fileName == null)
            {
                throw new ArgumentNullException("fileName");
            }

            return DTE.Solution.FindProjectItem(fileName);
        }

        /// <summary>
        /// This method is a part of T4 Toolbox infrastructure. Don't call it in your code.
        /// </summary>
        /// <param name="transformation">
        /// Instance of the <see cref="TextTransformation"/> class generated by T4 engine.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Method throws <see cref="ArgumentNullException"/> when the specified 
        /// <paramref name="transformation"/> is null.
        /// </exception>
        /// <remarks>
        /// During template transformation, this method is called by code in T4Toolbox.tt.
        /// </remarks>
        public static void OnTransformationStarted(TextTransformation transformation)
        {
            if (transformation == null)
            {
                throw new ArgumentNullException("transformation");
            }

            TransformationContext.transformation = transformation;
            TransformationContext.outputManager = new OutputManager();

            CreateTraceListener();
        }

        /// <summary>
        /// This method is a part of T4 Toolbox infrastructure. Don't call it in your code.
        /// </summary>
        /// <param name="transformation">
        /// Instance of the <see cref="TextTransformation"/> class generated by T4 engine.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Method throws <see cref="ArgumentNullException"/> when the specified 
        /// <paramref name="transformation"/> is null.
        /// </exception>
        /// <remarks>
        /// During template transformation, this method is called by code in T4Toolbox.tt.
        /// </remarks>
        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Type.InvokeMember", Justification = "Must call the internal GetDefaultDomain method.")]
        public static void OnTransformationEnded(TextTransformation transformation)
        {
            try
            {
                if (transformation == null)
                {
                    throw new ArgumentNullException("transformation");
                }

                if (TransformationContext.transformation != null && !TransformationContext.Errors.HasErrors)
                {
                    //Update the files in the default AppDomain to avoid remoting errors on Database projects
                    //BindingFlags invokeInternalStaticMethod = BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic;
                    //AppDomain defaultDomain = (AppDomain)typeof(AppDomain).InvokeMember("GetDefaultDomain", invokeInternalStaticMethod, null, null, null, CultureInfo.InvariantCulture);

                    //var bd1 = defaultDomain.BaseDirectory;

                    //var setup = new AppDomainSetup();
                    //setup.ApplicationBase = AppDomain.CurrentDomain.BaseDirectory;
                    //AppDomain serverAppDomain = AppDomain.CreateDomain("ServerAppDomain", null, setup);

                    //var udf = TransformationContext.outputManager;
                    //defaultDomain.DoCallBack(udf.UpdateFiles);

                    OutputProcessor.Host = Host;
                    outputManager.UpdateFiles();
                }

                TransformationContext.transformation = null;
                TransformationContext.outputManager = null;
                TransformationContext.project = null;
                TransformationContext.projectItem = null;
                TransformationContext.dte = null;

                if (TransformationContext.TransformationEnded != null)
                {
                    TransformationContext.TransformationEnded(null, EventArgs.Empty);
                }
            }
            catch (TransformationException e)
            {
                // Display expected errors in the Error List window without the call stack
                CompilerErrorCollection errors = new CompilerErrorCollection();
                CompilerError error = new CompilerError();
                error.ErrorText = e.Message;
                error.FileName = Host.TemplateFile;
                errors.Add(error);
                TransformationContext.Host.LogErrors(errors);
            }
            finally
            {
                DestroyTraceListener();
            }
        }

        /// <summary>
        /// This method is a part of T4 Toolbox infrastructure. Don't call it in your code.
        /// </summary>
        /// <remarks>
        /// Clears all code generation errors.
        /// </remarks>
        public static void ClearErrors()
        {
            TransformationContext.Errors.Clear();
        }

        /// <summary>
        /// This method is a part of T4 Toolbox infrastructure. Don't call it in your code.
        /// </summary>
        /// <param name="errors">
        /// A collection of <see cref="CompilerError"/> objects.
        /// </param>
        /// <remarks>
        /// Copies errors from the specified collection of <paramref name="errors"/> to the
        /// list of <see cref="TextTransformation.Errors"/> that will be displayed in Visual
        /// Studio error window.
        /// </remarks>
        public static void ReportErrors(CompilerErrorCollection errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException("errors");
            }

            foreach (CompilerError error in errors)
            {
                if (string.IsNullOrEmpty(error.FileName) && HostProperty != null)
                {
                    error.FileName = TransformationContext.Host.TemplateFile;
                }

                TransformationContext.Errors.Add(error);
            }
        }

        #region internal

        /// <summary>
        /// This method is a part of T4 Toolbox infrastructure. Don't call it in your code.
        /// </summary>
        /// <param name="content">
        /// Generated content.
        /// </param>
        /// <param name="output">
        /// An <see cref="OutputInfo"/> object that specifies how the content must be saved.
        /// </param>
        /// <param name="errors">
        /// A collection of <see cref="CompilerError"/> objects that represent errors and warnings
        /// that occurred while generating this content.
        /// </param>
        internal static void Render(string content, OutputInfo output, CompilerErrorCollection errors)
        {
            if (output.PreserveExistingFile == true)
            {
                output.PreserveExistingFile = false;
                CompilerError error = new CompilerError();
                error.IsWarning = true;
                // PreserveExistingFile depends on where it is put (ie before Render) - not atomic. Too risky.
                // this.PreserveExistingFile = true in a Template would be safer.
                // But I prefer explict method, as it is quite clear.
                error.ErrorText = "PreserveExistingFile is deprecated. Always use before Render() in Generator, or in template itself, or use RenderToFileIfNotExists(filename) instead.";
                error.FileName = Host.TemplateFile;
                errors.Add(error);
            }

            TransformationContext.ReportErrors(errors);
            TransformationContext.outputManager.Append(output, content, Host, Transformation);
        }

        /// <summary>
        /// Saves content to a file and adds it to the Visual Studio <see cref="Project"/>,
        /// only if the file does not already exist.
        /// </summary>
        /// Suggestion made by ggreig in Nov 2008, with thanks
        public static void RenderIfNotExists(string content, OutputInfo output, CompilerErrorCollection errors)
        {
            TransformationContext.ReportErrors(errors);

            string templateDirectory = System.IO.Path.GetDirectoryName(TransformationContext.Host.TemplateFile);
            string outputFilePath = System.IO.Path.Combine(templateDirectory, output.File);

            if (!System.IO.File.Exists(outputFilePath))
            {
                TransformationContext.outputManager.Append(output, content, Host, Transformation);
            }
        }

        #endregion

        #region private

        /// <summary>
        /// Creates a trace listener to redirect <see cref="Debug"/> and <see cref="Trace"/>
        /// logging to the <see cref="OutputWindow"/> of Visual Studio.
        /// </summary>
        private static void CreateTraceListener()
        {
            traceListener = new OutputWindowTraceListener("Transform text templates ");
            Trace.Listeners.Add(traceListener);
        }

        /// <summary>
        /// Destroys the <see cref="OutputWindowTraceListener"/> associated with <see cref="Debug"/>
        /// and <see cref="Trace"/>.
        /// </summary>
        private static void DestroyTraceListener()
        {
            Trace.Listeners.Remove(traceListener);
            traceListener = null;
        }

        #endregion
    }
}