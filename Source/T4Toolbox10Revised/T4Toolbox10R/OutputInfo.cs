// <copyright file="OutputInfo.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Stores information about a code generation output.
    /// </summary>
    [Serializable]
    public class OutputInfo
    {
        /// <summary>
        /// Stores the list of build properties for the output file.
        /// </summary>
        private IDictionary<string, string> buildProperties;

        /// <summary>
        /// Stores the list of assembly references required by the output file.
        /// </summary>
        private ICollection<string> references;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputInfo"/> class.
        /// </summary>
        public OutputInfo()
        {
            this.buildProperties = new Dictionary<string, string>();
            this.references = new List<string>();
            this.Encoding = Encoding.Default;
            this.File = string.Empty;
            this.Project = string.Empty;
        }

        /// <summary>
        /// Gets a list of assembly references required by the output file.
        /// </summary>
        /// <value>
        /// An <see cref="ICollection{String}"/> object where each item is an assembly 
        /// name or file path.
        /// </value>
        public ICollection<string> References
        {
            get { return this.references; }
        }

        /// <summary>
        /// Gets or sets encoding of the output file.
        /// </summary>
        /// <value>
        /// An <see cref="Encoding"/> object.
        /// </value>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Gets or sets path to the output file.
        /// </summary>
        /// <value>
        /// A <see cref="String"/> containing relative or absolute file path.
        /// </value>
        /// <remarks>
        /// When <see cref="File"/> is null or <see cref="string.Empty"/>, the
        /// template will add generated content to the output of the main T4 file.
        /// </remarks>
        public string File { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether existing output file 
        /// should be preserved during code generation.
        /// </summary>
        /// <value>
        /// <c>True</c> when existing file should be preserved or <c>False</c> otherwise.
        /// </value>
        /// <remarks>
        /// Set <see cref="PreserveExistingFile"/> to <c>true</c> when generating initial versions of the 
        /// source files that will be coded manually. This will cause the code generator to preserve the 
        /// existing file when the code generator runs for the first time as well as when the output file
        /// is no longer re-generated. 
        /// </remarks>
        public bool PreserveExistingFile { get; set; }

        /// <summary>
        /// Gets or sets path to the target project.
        /// </summary>
        /// <value>
        /// A <see cref="String"/> containing relative or absolute project file path.
        /// </value>
        public string Project { get; set; }

        /// <summary>
        /// Gets or sets the build action.
        /// </summary>
        /// <value>
        /// A <see cref="String"/> containing the name of the build action to apply to target item.
        /// </value>
        public string BuildAction { get; set; }

        /// <summary>
        /// Gets a dictionary of build properties of the output file.
        /// </summary>
        /// <value>
        /// A <see cref="IDictionary{String,String}"/> where each build property is 
        /// represented by a key/value pair.
        /// </value>
        public IDictionary<string, string> BuildProperties
        {
            get { return this.buildProperties; }
        }

        /// <summary>
        /// Gets or sets the action for copying a file to the output directory.
        /// </summary>
        /// <value>
        /// A <see cref="CopyToOutputDirectory"/> representing if or when a file is copied to the output directory.
        /// </value>
        public CopyToOutputDirectory CopyToOutputDirectory { get; set; }

        /// <summary>
        /// Gets or sets the name of the tool that transforms a file at design time and places
        /// the output of that transformation into another file.
        /// </summary>
        /// <value>
        /// A <see cref="String"/> containing the name of the custom tool to run for a project item.
        /// </value>
        public string CustomTool { get; set; }

        /// <summary>
        /// Gets or sets the namespace into which the output of the custom tool is placed.
        /// </summary>
        /// <value>
        /// A <see cref="String"/> containing the namespace for the output of the custom tool.
        /// </value>
        public string CustomToolNamespace { get; set; }

        /// <summary>
        /// Determines if two paths are the same.
        /// </summary>
        /// <param name="path1">First path to compare.</param>
        /// <param name="path2">Second path to compare.</param>
        /// <returns><c>true</c> if the two paths are the same.</returns>
        internal static bool SamePath(string path1, string path2)
        {
            return string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds new items to the <see cref="References"/>.
        /// </summary>
        /// <param name="references">
        /// An <see cref="ICollection{String}"/> object where each item is an assembly 
        /// name or file path.
        /// </param>
        internal void AppendReferences(ICollection<string> references)
        {
            foreach (string reference in references)
            {
                if (!this.References.Contains(reference))
                {
                    this.References.Add(reference);
                }
            }
        }

        /// <summary>
        /// Adds new items to the <see cref="BuildProperties"/>.
        /// </summary>
        /// <param name="buildProperties">
        /// An <see cref="IDictionary{String,String}"/> object where each 
        /// <see cref="KeyValuePair{String,String}"/> represents a build property.
        /// </param>
        internal void AppendBuildProperties(IDictionary<string, string> buildProperties)
        {
            foreach (KeyValuePair<string, string> buildProperty in buildProperties)
            {
                if (!this.BuildProperties.ContainsKey(buildProperty.Key))
                {
                    this.BuildProperties.Add(buildProperty.Key, buildProperty.Value);
                }
            }
        }
    }
}
