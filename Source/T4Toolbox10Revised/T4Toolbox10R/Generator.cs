// <copyright file="Generator.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using System;
    using System.CodeDom.Compiler;
    using System.Globalization;

    /// <summary>
    /// Abstract base class for code generators.
    /// </summary>
    public abstract class Generator
    {
        /// <summary>
        /// Stores collection of errors and warnings produced by this code generator.
        /// </summary>
        private CompilerErrorCollection errors = new CompilerErrorCollection();

        /// <summary>
        /// Gets collections of errors and warnings produced by the <see cref="Run"/> method.
        /// </summary>
        /// <value>
        /// A collection of <see cref="CompilerError"/> objects.
        /// </value>
        public CompilerErrorCollection Errors
        {
            get { return this.errors; }
        }

        /// <summary>
        /// Adds a new error to the list of <see cref="Errors"/> produced by the current <see cref="Run"/>.
        /// </summary>
        /// <param name="message">
        /// Error message.
        /// </param>
        public void Error(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            CompilerError error = new CompilerError();
            error.ErrorText = message;
            this.Errors.Add(error);
        }

        /// <summary>
        /// Adds a new error to the list of <see cref="Errors"/> produced by the current <see cref="Run"/>.
        /// </summary>
        /// <param name="format">
        /// A <see cref="string.Format(string, object)"/> string of the error message.
        /// </param>
        /// <param name="args">
        /// An array of one or more <paramref name="format"/> arguments.
        /// </param>
        public void Error(string format, params object[] args)
        {
            this.Error(string.Format(CultureInfo.CurrentCulture, format, args));
        }

        /// <summary>
        /// Validates and runs the generator.
        /// </summary>
        public void Run()
        {
            this.Errors.Clear();

            try
            {
                this.Validate();
                if (!this.Errors.HasErrors)
                {
                    this.RunCore();
                }
            }
            catch (TransformationException e)
            {
                this.Error(e.Message);
            }

            TransformationContext.ReportErrors(this.Errors);
        }

        /// <summary>
        /// Adds a new warning to the list of <see cref="Errors"/> produced by the current <see cref="Run"/>.
        /// </summary>
        /// <param name="message">
        /// Warning message.
        /// </param>
        public void Warning(string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            CompilerError warning = new CompilerError();
            warning.IsWarning = true;
            warning.ErrorText = message;
            this.Errors.Add(warning);
        }

        /// <summary>
        /// Adds a new warning to the list of <see cref="Errors"/> produced by the current <see cref="Run"/>.
        /// </summary>
        /// <param name="format">
        /// A <see cref="string.Format(string, object)"/> string of the warning message.
        /// </param>
        /// <param name="args">
        /// An array of one or more <paramref name="format"/> arguments.
        /// </param>
        public void Warning(string format, params object[] args)
        {
            this.Warning(string.Format(CultureInfo.CurrentCulture, format, args));
        }

        /// <summary>
        /// When overridden in a derived class, generates output files.
        /// </summary>
        /// <remarks>
        /// Override this method in derived classes to <see cref="Template.Render"/> 
        /// one or more <see cref="Template"/>s. Note that this method will not be executed
        /// if <see cref="Validate"/> method produces one or more <see cref="Errors"/>.
        /// </remarks>
        protected abstract void RunCore();

        /// <summary>
        /// When overridden in a derived class, validates parameters of the generator.
        /// </summary>
        /// <remarks>
        /// Override this method in derived classes to validate required and optional
        /// parameters of this <see cref="Generator"/>. Call <see cref="Error(string)"/>, 
        /// <see cref="Warning(string)"/> methods or throw <see cref="TransformationException"/> 
        /// to report errors.
        /// </remarks>
        protected virtual void Validate()
        {
            // This method is intentionally left blank.
        }
    }
}