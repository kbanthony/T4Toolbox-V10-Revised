// <copyright file="OutputWindowTraceListener.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using EnvDTE;
    using EnvDTE80;
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Simple TraceListener class that focuses the T4 Editor output window and writes debug messages to it.
    /// </summary>
    internal class OutputWindowTraceListener : TraceListener
    //JSR public class OutputWindowTraceListener : TraceListener
    {
        /// <summary>
        /// Name of the output pane where messages are displayed.
        /// </summary>
        private readonly string paneName;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputWindowTraceListener"/> class.
        /// </summary>
        /// <param name="paneName">
        /// Name of the <see cref="OutputWindowPane"/> where the messages will be written.
        /// </param>
        public OutputWindowTraceListener(string paneName)
        {
            this.paneName = paneName;
        }

        /// <summary>
        /// Writes a message to the Visual Studio <see cref="OutputWindow"/>.
        /// </summary>
        /// <param name="message">The message to write to output.</param>
        public override void Write(string message)
        {
            OutputWindow outputWindow = ((DTE2)TransformationContext.DTE).ToolWindows.OutputWindow;

            OutputWindowPane pane;
            try
            {
                pane = outputWindow.OutputWindowPanes.Item(this.paneName);
            }
            catch (ArgumentException)
            {
                pane = outputWindow.OutputWindowPanes.Add(this.paneName);
            }

            pane.Activate();
            pane.OutputString(message);
        }

        /// <summary>
        /// Writes a message to the Visual Studio <see cref="OutputWindow"/>.
        /// </summary>
        /// <param name="message">The message to write to output.</param>
        public override void WriteLine(string message)
        {
            this.Write(message + Environment.NewLine);
        }
    }
}