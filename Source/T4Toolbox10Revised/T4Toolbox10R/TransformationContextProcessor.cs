// <copyright file="TransformationContextProcessor.cs" company="T4 Toolbox Team">
//  Copyright © T4 Toolbox Team. All Rights Reserved.
// </copyright>

namespace T4Toolbox
{
    using System.CodeDom;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;

    /// <summary>
    /// This directive processor is a part of the T4 Toolbox infrastructure. Don't
    /// use it in your templates.
    /// </summary>
    /// <remarks>
    /// This directive generates code necessary to initialize the static 
    /// <see cref="TransformationContext"/> class in the GeneratedTextTransformation.
    /// </remarks>
    public class TransformationContextProcessor : DirectiveProcessor
    {
        /// <summary>
        /// Gets the directive name as it is supposed to be used in template code.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that contains name of the TransformationContext directive.
        /// </value>
        protected override string DirectiveName
        {
            get { return "TransformationContext"; }
        }

        /// <summary>
        /// Generates constructor and Dispose method for the GeneratedTextTransformation class.
        /// </summary>
        /// <param name="directiveName">
        /// The name of the directive to process. 
        /// </param>
        /// <param name="arguments">
        /// The arguments for the directive. 
        /// </param>
        public override void ProcessDirective(string directiveName, IDictionary<string, string> arguments)
        {
            // Make sure T4 references correct version of the T4Toolbox.dll when compiling the template
            // this.GetType().Assembly.FullName;

            //Changed to absolute path
            var path = this.GetType().Assembly.Location;

            this.References.Add(path);

            base.ProcessDirective(directiveName, arguments);

            this.GenerateTransformationContext();
            this.GenerateConstructor();
            this.GenerateDisposeMethod();
        }

        /// <summary>
        /// Generates a derived <see cref="TransformationContext"/> with a strongly-typed
        /// <see cref="TransformationContext.Transformation"/> property.
        /// </summary>
        private void GenerateTransformationContext()
        {
            //// public abstract class TransformationContext : T4Toolbox.TransformationContext {
            CodeTypeDeclaration transformationContext = new CodeTypeDeclaration("TransformationContext");
            transformationContext.TypeAttributes = TypeAttributes.Abstract | TypeAttributes.Public;
            transformationContext.BaseTypes.Add(new CodeTypeReference(typeof(TransformationContext)));

            ////     public new static GeneratedTextTransformation Transformation {
            CodeMemberProperty transformation = new CodeMemberProperty();
            transformation.Attributes = MemberAttributes.Public | MemberAttributes.New | MemberAttributes.Static;
            transformation.Type = new CodeTypeReference("GeneratedTextTransformation");
            transformation.Name = "Transformation";
            transformationContext.Members.Add(transformation);

            ////         get { return (GeneratedTextTransformation)T4Toolbox.TransformationContext.Transformation; }  
            ////     }
            CodePropertyReferenceExpression propertyReference = new CodePropertyReferenceExpression(
                new CodeTypeReferenceExpression(typeof(TransformationContext)),
                "Transformation");
            CodeCastExpression castExpression = new CodeCastExpression("GeneratedTextTransformation", propertyReference);
            transformation.GetStatements.Add(new CodeMethodReturnStatement(castExpression));

            //// }
            this.LanguageProvider.GenerateCodeFromType(transformationContext, this.ClassCode, null);
        }

        /// <summary>
        /// Generates a constructor for the GeneratedTextTransformation class.
        /// </summary>
        /// <remarks>
        /// This constructor is a part of T4 Toolbox infrastructure. By providing this
        /// constructor we are tricking T4 to execute our code in the beginning of the
        /// template transformation. This approach takes advantage of T4 not generating
        /// a default constructor and may break in the future.
        /// </remarks>
        private void GenerateConstructor()
        {
            CodeNamespace @namespace = new CodeNamespace("TemplatingAppDomain");
            @namespace.Imports.Add(new CodeNamespaceImport("System"));
            @namespace.Imports.Add(new CodeNamespaceImport("T4Toolbox"));

            CodeTypeDeclaration @class = new CodeTypeDeclaration("GeneratedTextTransformation");
            @namespace.Types.Add(@class);

            //// public GeneratedTextTransformation() {
            CodeConstructor constructor = new CodeConstructor();
            constructor.Attributes = MemberAttributes.Public;
            @class.Members.Add(constructor);

            ////    TransformationContext.OnTransformationStarted(this);
            CodeMethodInvokeExpression onTransformationStarted = new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression(typeof(TransformationContext).Name),
                "OnTransformationStarted",
                new CodeThisReferenceExpression());
            constructor.Statements.Add(onTransformationStarted);

            //// }
            this.GenerateCodeFromConstructor(constructor, @class, @namespace, null);
        }

        /// <summary>
        /// Generates a Dispose method for the GeneratedTextTransformation class.
        /// </summary>
        /// <remarks>
        /// This method is a part of T4 Toolbox infrastructure. By overriding this method
        /// we are tricking T4 to execute our code in the end of the template transformation.
        /// This approach takes advantage of T4 not generating a Dispose method and may
        /// break in the future.
        /// </remarks>
        private void GenerateDisposeMethod()
        {
            //// protected override void Dispose(bool disposing) {
            CodeMemberMethod disposeMethod = new CodeMemberMethod();
            disposeMethod.Name = "Dispose";
            disposeMethod.Attributes = MemberAttributes.Family | MemberAttributes.Override;
            CodeParameterDeclarationExpression disposingArgument = new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(bool)), "disposing");
            disposeMethod.Parameters.Add(disposingArgument);

            ////    if (disposing) {
            CodeConditionStatement ifStatement = new CodeConditionStatement();
            ifStatement.Condition = new CodeArgumentReferenceExpression(disposingArgument.Name);
            disposeMethod.Statements.Add(ifStatement);

            ////        TransformationContext.OnTransformationEnded(this);
            CodeMethodInvokeExpression onTransformationStarted = new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression(typeof(TransformationContext).Name),
                "OnTransformationEnded",
                new CodeThisReferenceExpression());
            ifStatement.TrueStatements.Add(onTransformationStarted);

            ////    }
            //// }
            this.LanguageProvider.GenerateCodeFromMember(disposeMethod, this.ClassCode, null);
        }

        /// <summary>
        /// Generates code from the specified <paramref name="constructor"/>.
        /// </summary>
        /// <param name="constructor">Class constructor for which code needs to be generated.</param>
        /// <param name="type">Type declaration.</param>
        /// <param name="namespace">Namespace declaration.</param>
        /// <param name="options">Code generation options.</param>
        /// <remarks>
        /// This method is a workaround for <see cref="CodeDomProvider.GenerateCodeFromMember"/> 
        /// not generating constructors properly.
        /// </remarks>
        private void GenerateCodeFromConstructor(
            CodeConstructor constructor,
            CodeTypeDeclaration type,
            CodeNamespace @namespace,
            CodeGeneratorOptions options)
        {
            const string StartMarker = "___startMarker___";
            const string EndMarker = "___endMarker___";

            // Insert marker fields around the target constructor
            int indexOfMember = type.Members.IndexOf(constructor);
            type.Members.Insert(indexOfMember + 1, new CodeMemberField(typeof(int), EndMarker));
            type.Members.Insert(indexOfMember, new CodeMemberField(typeof(int), StartMarker));

            using (StringWriter buffer = new StringWriter(CultureInfo.InvariantCulture))
            {
                // Generate type declaration in verbatim order to preserve placement of marker fields
                options = options ?? new CodeGeneratorOptions();
                options.VerbatimOrder = true;
                this.LanguageProvider.GenerateCodeFromNamespace(@namespace, buffer, options);

                // Extract constructor code from the generated type code
                const string ConstructorCode = "constructor";
                Regex regex = new Regex(
                    @"^[^\r\n]*" + StartMarker + @"[^\n]*$" +
                    @"(?<" + ConstructorCode + @">.*)" +
                    @"^[^\r\n]*" + EndMarker + @"[^\n]*$",
                    RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
                string code = regex.Match(buffer.ToString()).Groups[ConstructorCode].Value;

                // Write constructor code to the output buffer
                this.ClassCode.Write(code);
            }
        }
    }
}
