// <copyright file="OutputFile.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using System;
    using System.Text;

    /// <summary>
    /// This class is a part of T4 Toolbox infrastructure. Don't use it in your code.
    /// </summary>
    /// <remarks>
    /// This class contains information about generated output file.
    /// </remarks>
    [Serializable]
    internal class OutputFile : OutputInfo
    //JSR public class OutputFile : OutputInfo
    {
        /// <summary>
        /// Stores content of the output file.
        /// </summary>
        private readonly StringBuilder content = new StringBuilder();

        /// <summary>
        /// Gets content of the output file.
        /// </summary>
        public StringBuilder Content
        {
            get { return this.content; }
        }
    }
}
