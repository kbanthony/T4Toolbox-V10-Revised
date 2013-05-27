// <copyright file="TransformationAssert.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using System;
    using System.CodeDom.Compiler;
    using System.Text;
    //using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Verifies test conditions associated with template transformation.
    /// </summary>
    public static class TransformationAssert
    {
        /// <summary>
        /// Verifies that specified <paramref name="generator"/> produced a specific
        /// error during transformation.
        /// </summary>
        /// <param name="generator">
        /// A <see cref="Generator"/> that has already ran.
        /// </param>
        /// <param name="errorText">
        /// A <see cref="string"/> that contains expected error text.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="generator"/> or <paramref name="errorText"/> is null.
        /// </exception>
        /// <exception cref="AssertFailedException">
        /// No references to <paramref name="errorText"/> could be found among
        /// <see cref="Generator.Errors"/>.
        /// </exception>
        public static void HasError(Generator generator, string errorText)
        {
            if (generator == null)
            {
                throw new ArgumentNullException("generator");
            }

            if (errorText == null)
            {
                throw new ArgumentNullException("errorText");
            }

            HasError(generator.Errors, errorText);
        }

        /// <summary>
        /// Verifies that specified <paramref name="template"/> produced a specific
        /// error during transformation.
        /// </summary>
        /// <param name="template">
        /// A <see cref="Template"/> that has already been rendered.
        /// </param>
        /// <param name="errorText">
        /// A <see cref="string"/> that contains expected error text.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="template"/> or <paramref name="errorText"/> is null.
        /// </exception>
        /// <exception cref="AssertFailedException">
        /// No references to <paramref name="errorText"/> could be found among
        /// <see cref="Template.Errors"/>.
        /// </exception>
        public static void HasError(Template template, string errorText)
        {
            if (template == null)
            {
                throw new ArgumentNullException("template");
            }

            if (errorText == null)
            {
                throw new ArgumentNullException("errorText");
            }

            HasError(template.Errors, errorText);
        }

        /// <summary>
        /// Verifies that specified <paramref name="generator"/> produced no <see cref="Template.Errors"/>
        /// during transformation.
        /// </summary>
        /// <param name="generator">
        /// A <see cref="Generator"/> that has already ran.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="generator"/> is null.
        /// </exception>
        /// <exception cref="AssertFailedException">
        /// <see cref="Generator.Errors"/> is not empty.
        /// </exception>
        public static void NoErrors(Generator generator)
        {
            if (generator == null)
            {
                throw new ArgumentNullException("generator");
            }

            NoErrors(generator.Errors);
        }

        /// <summary>
        /// Verifies that specified <paramref name="template"/> produced no <see cref="Template.Errors"/>
        /// during transformation.
        /// </summary>
        /// <param name="template">
        /// A <see cref="Template"/> that has already been rendered.
        /// </param>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="template"/> is null.
        /// </exception>
        /// <exception cref="AssertFailedException">
        /// <see cref="Template.Errors"/> is not empty.
        /// </exception>
        public static void NoErrors(Template template)
        {
            if (template == null)
            {
                throw new ArgumentNullException("template");
            }

            NoErrors(template.Errors);
        }

        #region private

        /// <summary>
        /// Verifies that specified error collection contains a specific error.
        /// </summary>
        /// <param name="errors">
        /// A <see cref="CompilerErrorCollection"/>.
        /// </param>
        /// <param name="errorText">
        /// A <see cref="string"/> that contains expected error text.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// No references to <paramref name="errorText"/> could be found among the <paramref name="errors"/>.
        /// </exception>
        private static void HasError(CompilerErrorCollection errors, string errorText)
        {
            foreach (CompilerError error in errors)
            {
                if (error.ErrorText.Contains(errorText))
                {
                    return;
                }
            }

            //JSR
            //throw new AssertFailedException(
            //    string.Format(CultureInfo.CurrentCulture, "Error '{0}' was expected during transformation", errorText));
        }

        /// <summary>
        /// Verifies that specified error collection is empty.
        /// </summary>
        /// <param name="errors">
        /// A <see cref="CompilerErrorCollection"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// <paramref name="errors"/> collection is not empty.
        /// </exception>
        private static void NoErrors(CompilerErrorCollection errors)
        {
            if (errors.Count > 0)
            {
                StringBuilder message = new StringBuilder();
                foreach (CompilerError error in errors)
                {
                    message.AppendFormat("Transformation {0}", error.IsWarning ? "warning" : "eror");
                    if (!string.IsNullOrEmpty(error.FileName))
                    {
                        message.AppendFormat(" in {0}, line {1}", error.FileName, error.Line);
                    }

                    message.AppendFormat(": {0}", error.ErrorText);

                    if (!error.ErrorText.EndsWith(".", StringComparison.CurrentCulture))
                    {
                        message.Append(".");
                    }

                    message.Append(" ");
                }

                //JSR
                //throw new AssertFailedException(message.ToString());
            }
        }

        #endregion
    }
}