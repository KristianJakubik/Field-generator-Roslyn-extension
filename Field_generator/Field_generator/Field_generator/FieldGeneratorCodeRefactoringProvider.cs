using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace FieldGenerator
{
	[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(FieldGeneratorCodeRefactoringProvider)), Shared]
	internal class FieldGeneratorCodeRefactoringProvider : CodeRefactoringProvider
	{
		private int field;

		public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
		{
			// TODO: Replace the following code with your own analysis, generating a CodeAction for each refactoring to offer

			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			// Find the node at the selection.
			var node = root.FindNode(context.Span);

			// Only offer a refactoring if the selected node is a type declaration node.
			if (!(node is ParameterSyntax parameterSyntax))
			{
				return;
			}

			var parameterNode = (ParameterSyntax)node;
			var underscoreName = $"_{parameterNode.Identifier}";

			// For any type declaration node, create a code action to reverse the identifier text.
			var action = CodeAction.Create($"Create and initialize field {underscoreName}", c => CreateInitilizeFieldAsync(context.Document, parameterSyntax, underscoreName, c));

			// Register this code action.
			context.RegisterRefactoring(action);
		}

		private async Task<Document> CreateInitilizeFieldAsync(Document document, ParameterSyntax parameter, string underscoreName, CancellationToken cancellationToken)
		{
			var oldClass = parameter.Ancestors()
							.OfType<ClassDeclarationSyntax>()
							.First();

			var oldConstructor = parameter.Ancestors()
									.OfType<ConstructorDeclarationSyntax>()
									.First();

			StatementSyntax initilizationStatement = SyntaxFactory.ExpressionStatement(
														SyntaxFactory.AssignmentExpression(
															SyntaxKind.SimpleAssignmentExpression,
															SyntaxFactory.IdentifierName(underscoreName),
															SyntaxFactory.IdentifierName(parameter.Identifier)));
		
			var newConstructor = oldConstructor.WithBody(
									oldConstructor.Body.AddStatements(initilizationStatement));
			var classWithNewConstructor = oldClass.ReplaceNode(oldConstructor, newConstructor);


			var newFieldDeclaration = SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(parameter.Type))
										.WithModifiers(SyntaxFactory.TokenList(
														SyntaxFactory.Token(SyntaxKind.PrivateKeyword),
														SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword)))
										.WithAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.VariableDeclarator(underscoreName)));

			var fieldDeclaration = classWithNewConstructor.Members
													.OfType<FieldDeclarationSyntax>()
													.ToList();
			fieldDeclaration.Add(newFieldDeclaration);

			var classWithNewConstructorAndField = classWithNewConstructor.AddMembers(newFieldDeclaration);

			var oldRoot = await document.GetSyntaxRootAsync(cancellationToken)
									.ConfigureAwait(false);

			var newRoot = oldRoot.ReplaceNode(oldClass, classWithNewConstructorAndField);				

			return document.WithSyntaxRoot(newRoot);
		}
	}
}
