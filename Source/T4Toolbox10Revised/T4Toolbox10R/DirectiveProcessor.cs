// <copyright file="DirectiveProcessor.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using Microsoft.VisualStudio.TextTemplating;
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Base class for directive processors.
    /// </summary>
    public abstract class DirectiveProcessor : Microsoft.VisualStudio.TextTemplating.DirectiveProcessor, IDisposable
    {
        #region fields

        /// <summary>
        /// T4 engine host.
        /// </summary>
        private static ITextTemplatingEngineHost host;

        /// <summary>
        /// CodeDom language provider, used to generate .NET code from XML schema.
        /// </summary>
        private CodeDomProvider languageProvider;

        /// <summary>
        /// Buffer for class code.
        /// </summary>
        private StringWriter classCode;

        /// <summary>
        /// Buffer for namespace imports.
        /// </summary>
        private List<string> imports;

        /// <summary>
        /// Buffer for pre-initialization code.
        /// </summary>
        private StringWriter preInitializationCode;

        /// <summary>
        /// Buffer for post-initialization code.
        /// </summary>
        private StringWriter postInitializationCode;

        /// <summary>
        /// Buffer for assembly references.
        /// </summary>
        private List<string> references;

        #endregion

        /// <summary>
        /// Gets a T4 engine host.
        /// </summary>
        /// <value>
        /// A <see cref="ITextTemplatingEngineHost"/> object obtained by the 
        /// <see cref="Initialize"/> method.
        /// </value>
        protected static ITextTemplatingEngineHost Host
        {
            get { return host; }
            set { host = value; }
        }

        /// <summary>
        /// Gets the class code buffer.
        /// </summary>
        /// <value>
        /// A <see cref="StringWriter"/> object that serves as a buffer for generated code.
        /// </value>
        protected StringWriter ClassCode
        {
            get { return this.classCode; }
        }

        /// <summary>
        /// Gets the directive name.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that contains directive name.
        /// </value>
        /// <remarks>
        /// Override this property in the derived class to indicate which directive 
        /// the processor will handle.
        /// </remarks>
        protected abstract string DirectiveName { get; }

        /// <summary>
        /// Gets the namespace imports buffer.
        /// </summary>
        /// <value>
        /// A <see cref="ICollection{String}"/> of namespaces.
        /// </value>
        protected ICollection<string> Imports
        {
            get { return this.imports; }
        }

        /// <summary>
        /// Gets the post-initialization code buffer.
        /// </summary>
        /// <value>
        /// A <see cref="StringWriter"/> object that serves as a buffer for generated code.
        /// </value>
        protected StringWriter PostInitializationCode
        {
            get { return this.postInitializationCode; }
        }

        /// <summary>
        /// Gets the pre-initialization code buffer.
        /// </summary>
        /// <value>
        /// A <see cref="StringWriter"/> object that serves as a buffer for generated code.
        /// </value>
        protected StringWriter PreInitializationCode
        {
            get { return this.preInitializationCode; }
        }

        /// <summary>
        /// Gets the assembly references buffer.
        /// </summary>
        /// <value>
        /// A <see cref="ICollection{String}"/> of assembly references.
        /// </value>
        protected ICollection<string> References
        {
            get { return this.references; }
        }

        /// <summary>
        /// Gets a language provider.
        /// </summary>
        /// <value>
        /// A <see cref="CodeDomProvider"/> object obtained by the <see cref="StartProcessingRun"/> method.
        /// </value>
        protected CodeDomProvider LanguageProvider
        {
            get { return this.languageProvider; }
        }

        /// <summary>
        /// Releases disposable resources owend by the directive processor.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// This method is not used and left blank.
        /// </summary>
        public override void FinishProcessingRun()
        {
            // Release external references received from T4
            this.languageProvider = null;

            // DO NOT release the internal buffers. T4 Engine accesses them between the runs.
        }

        /// <summary>
        /// This method is not used and returns an empty string.
        /// </summary>
        /// <returns>
        /// A <see cref="string"/> that contains the code to add to the generated 
        /// transformation class.
        /// </returns>
        public override string GetClassCodeForProcessingRun()
        {
            return this.classCode.ToString();
        }

        /// <summary>
        /// This method is not used and returns an empty array.
        /// </summary>
        /// <returns>
        /// An array of type <see cref="string"/> that contains the namespaces. 
        /// </returns>
        public override string[] GetImportsForProcessingRun()
        {
            return this.imports.Distinct().ToArray();
        }

        /// <summary>
        /// This method is not used and returns an empty string.
        /// </summary>
        /// <returns>
        /// A <see cref="String"/> that contains the code to add to the generated 
        /// transformation class. 
        /// </returns>
        public override string GetPostInitializationCodeForProcessingRun()
        {
            return this.postInitializationCode.ToString();
        }

        /// <summary>
        /// This method is not used and returns an empty string.
        /// </summary>
        /// <returns>
        /// A <see cref="String"/> that contains the code to add to the generated 
        /// transformation class. 
        /// </returns>
        public override string GetPreInitializationCodeForProcessingRun()
        {
            return this.preInitializationCode.ToString();
        }

        /// <summary>
        /// This method is not used and returns an empty array.
        /// </summary>
        /// <returns>
        /// An array of type <see cref="String"/> that contains the references.
        /// </returns>
        public override string[] GetReferencesForProcessingRun()
        {
            return this.references.Distinct().ToArray();
        }

        /// <summary>
        /// T4 <see cref="Microsoft.VisualStudio.TextTemplating.Engine"/> calls this 
        /// method in the beginning of template transformation.
        /// </summary>
        /// <param name="host">
        /// The <see cref="ITextTemplatingEngineHost"/> object hosting the transformation.
        /// </param>
        /// <remarks>
        /// This method stores the <paramref name="host"/> in a static field to allow the 
        /// <see cref="OutputProcessor"/> to access it later during code generation.
        /// </remarks>    
        public override void Initialize(ITextTemplatingEngineHost host)
        {
            base.Initialize(host);
            DirectiveProcessor.host = host;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="directiveName"/> is "t4toolbox".
        /// </summary>
        /// <param name="directiveName">Name of the directive.</param>
        /// <returns>
        /// <c>true</c> if the directive is supported by the processor; otherwise, <c>false</c>. 
        /// </returns>
        public override bool IsDirectiveSupported(string directiveName)
        {
            if (directiveName == null)
            {
                throw new ArgumentNullException("directiveName");
            }

            return string.Compare(this.DirectiveName, directiveName, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// This method is not used and left blank.
        /// </summary>
        /// <param name="directiveName">
        /// The name of the directive to process. 
        /// </param>
        /// <param name="arguments">
        /// The arguments for the directive. 
        /// </param>
        public override void ProcessDirective(string directiveName, IDictionary<string, string> arguments)
        {
            // Validate directiveName argument
            if (!this.IsDirectiveSupported(directiveName))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Unsupported directive name: '{0}'. Please use '{1}' instead.",
                        directiveName,
                        this.DirectiveName),
                    "directiveName");
            }

            // Validate arguments argument
            if (arguments == null)
            {
                throw new ArgumentNullException("arguments");
            }
        }

        /// <summary>
        /// Begins a round of directive processing.
        /// </summary>
        /// <param name="languageProvider">CodeDom language provider for generating code.</param>
        /// <param name="templateContents">Contents of the T4 template.</param>
        /// <param name="errors">Compiler Errors.</param>
        public override void StartProcessingRun(CodeDomProvider languageProvider, string templateContents, CompilerErrorCollection errors)
        {
            // Validate parameters
            if (languageProvider == null)
            {
                throw new ArgumentNullException("languageProvider");
            }

            if (errors == null)
            {
                throw new ArgumentNullException("errors");
            }

            base.StartProcessingRun(languageProvider, templateContents, errors);

            // Initialize references to external objects provided by T4
            this.languageProvider = languageProvider;

            // Clean up buffers here instead of FinishProcessingRun because T4 uses them between the runs
            this.CleanupBuffers();
            this.InitializeBuffers();
        }

        /// <summary>
        /// Disposes managed resources owned by this directive processor.
        /// </summary>
        /// <param name="disposing">
        /// This parameter is always true. It is provided for consistency with <see cref="IDisposable"/> pattern.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.CleanupBuffers();
            }
        }

        /// <summary>
        /// Cleans up buffer objects used during code generation.
        /// </summary>
        private void CleanupBuffers()
        {
            this.imports = null;
            this.references = null;

            if (this.classCode != null)
            {
                this.classCode.Dispose();
                this.classCode = null;
            }

            if (this.postInitializationCode != null)
            {
                this.postInitializationCode.Dispose();
                this.postInitializationCode = null;
            }

            if (this.preInitializationCode != null)
            {
                this.preInitializationCode.Dispose();
                this.preInitializationCode = null;
            }
        }

        /// <summary>
        /// Initializes buffer objects used during code generation.
        /// </summary>
        private void InitializeBuffers()
        {
            this.imports = new List<string>();
            this.references = new List<string>();

            this.classCode = new StringWriter(CultureInfo.InvariantCulture);
            this.postInitializationCode = new StringWriter(CultureInfo.InvariantCulture);
            this.preInitializationCode = new StringWriter(CultureInfo.InvariantCulture);
        }
    }
}
