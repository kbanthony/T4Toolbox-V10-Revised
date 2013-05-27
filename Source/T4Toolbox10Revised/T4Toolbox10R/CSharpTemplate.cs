// <copyright file="CSharpTemplate.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Serves as a base class for templates that produce C# code.
    /// </summary>
    public abstract class CSharpTemplate : Template
    {
        /// <summary>
        /// Contains C# reserved keywords defined in MSDN documentation at
        /// http://msdn.microsoft.com/en-us/library/x53a06bb.aspx.
        /// </summary>
        private static readonly string[] reservedKeywords =
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
            "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
            "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
            "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
            "object", "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
            "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "volatile", "void", "while"
        };

        /// <summary>
        /// Converts the specified entity name to a valid C# field name.
        /// </summary>
        /// <param name="name">
        /// The original entity name specified in the model.
        /// </param>
        /// <returns>
        /// A camelCased identifier.
        /// </returns>
        public static string FieldName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            name = name.Trim();
            name = Char.ToLowerInvariant(name[0]) + name.Substring(1);
            return Identifier(name);
        }

        /// <summary>
        /// Returns a valid C# identifier for the specified name.
        /// </summary>
        /// <param name="name">
        /// The original entity name specified in the model.
        /// </param>
        /// <returns>
        /// A valid C# identifier based on rules described at 
        /// http://msdn.microsoft.com/en-us/library/aa664670.aspx.
        /// </returns>
        public static string Identifier(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }
            
            StringBuilder builder = new StringBuilder();

            char c = '\x0000';     // current character within name
            int i = 0;             // current index within name

            // Skip invalid characters from the beginning of the name 
            while (i < name.Length)
            {
                c = name[i++];

                // First character must be a letter or _
                if (Char.IsLetter(c) || c == '_')
                {
                    break;
                }
            }

            if (i <= name.Length)
            {
                builder.Append(c);
            }

            bool capitalizeNext = false;

            // Strip invalid characters from the remainder of the name and convert it to camelCase
            while (i < name.Length)
            {
                c = name[i++];

                // Subsequent character can be a letter, a digit, combining, connecting or formatting character
                UnicodeCategory category = Char.GetUnicodeCategory(c);
                if (!Char.IsLetterOrDigit(c) &&
                    category != UnicodeCategory.SpacingCombiningMark &&
                    category != UnicodeCategory.ConnectorPunctuation &&
                    category != UnicodeCategory.Format)
                {
                    capitalizeNext = true;
                    continue;
                }

                if (capitalizeNext)
                {
                    c = Char.ToUpperInvariant(c);
                    capitalizeNext = false;
                }

                builder.Append(c);
            }

            string identifier = builder.ToString();

            // If identifier is a reserved C# keyword
            if (Array.BinarySearch(reservedKeywords, identifier) >= 0)
            {
                // Convert it to literal identifer
                return "@" + identifier;
            }

            return identifier;
        }

        /// <summary>
        /// Converts the specified entity name to a valid C# property name.
        /// </summary>
        /// <param name="name">
        /// The original entity name specified in the model.
        /// </param>
        /// <returns>
        /// A PascalCased identifier.
        /// </returns>
        public static string PropertyName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            name = name.Trim();
            name = Char.ToUpperInvariant(name[0]) + name.Substring(1);
            return Identifier(name);
        }

        /// <summary>
        /// Generates the "autogenerated" file header.
        /// </summary>
        /// <returns>
        /// Generated content.
        /// </returns>
        public override string TransformText()
        {
            this.WriteLine("// <autogenerated>");
            this.WriteLine("//   This file was generated by T4 code generator {0}.", Path.GetFileName(TransformationContext.Host.TemplateFile));
            this.WriteLine("//   Any changes made to this file manually will be lost next time the file is regenerated.");
            this.WriteLine("// </autogenerated>");
            this.WriteLine(string.Empty);
            return this.GenerationEnvironment.ToString();
        }
    }
}