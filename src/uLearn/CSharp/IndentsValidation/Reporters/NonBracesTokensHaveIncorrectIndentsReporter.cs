using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace uLearn.CSharp.IndentsValidation.Reporters
{
	internal static class NonBracesTokensHaveIncorrectIndentsReporter
	{
		public static IEnumerable<string> Report(SyntaxTree tree)
		{
			return tree.GetRoot().DescendantNodes()
				.Where(NeedToValidateNonBracesTokens)
				.Select(x => SyntaxNodeOrToken.Create(tree, x, true))
				.SelectMany(CheckNonBracesStatements)
				.Distinct();
		}

		private static IEnumerable<string> CheckNonBracesStatements(SyntaxNodeOrToken rootStatementSyntax)
		{
			var rootLine = rootStatementSyntax.GetStartLine();
			var rootEndLine = rootStatementSyntax.GetConditionEndLine();
			var parent = rootStatementSyntax.GetParent();
			var rootStart = rootStatementSyntax.GetValidationStartIndexInSpaces();

			if (parent != null && parent.Kind == SyntaxKind.Block)
			{
				var parentLine = parent.GetStartLine();
				if (parentLine == rootLine)
					yield break;
				var parentStart = parent.GetValidationStartIndexInSpaces();
				if (rootStart == 0)
					yield break;
				var validateIndent = ValidateIndent(rootStatementSyntax, rootStart, parentStart, rootLine, parentLine);
				if (validateIndent != null)
				{
					yield return BaseStyleValidator.Report(rootStatementSyntax, validateIndent);
					yield break;
				}
			}

			var statementClauses = rootStatementSyntax.SyntaxNode.CreateStatements().ToArray();
			var reports = ValidateStatementClauses(statementClauses, rootStatementSyntax, rootStart, rootLine, rootEndLine);
			foreach (var report in reports)
				yield return report;
		}

		private static IEnumerable<string> ValidateStatementClauses(
			SyntaxNodeOrToken[] statementClauses,
			SyntaxNodeOrToken rootStatementSyntax,
			int rootStart,
			int rootLine,
			int rootEndLine)
		{
			foreach (var statementClause in statementClauses)
			{
				if (statementClause.Kind == SyntaxKind.Block)
					break;
				var statementStart = statementClause.GetValidationStartIndexInSpaces();
				var statementLine = statementClause.GetStartLine();

				if (statementClause.HasExcessNewLines())
				{
					yield return BaseStyleValidator.Report(statementClause,
						$"��������� �� ������ ����� ������ �������� ����� ����� �������� ({GetNodePosition(rootStatementSyntax)}).");
					continue;
				}
				if (!statementClause.OnSameIndentWithParent.HasValue)
				{
					if (statementStart != rootStart)
					{
						var report = ValidateIndent(rootStatementSyntax, statementStart, rootStart, statementLine, rootLine);
						if (report != null)
						{
							yield return BaseStyleValidator.Report(statementClause, report);
							continue;
						}
					}
				}
				else
				{
					if (statementClause.OnSameIndentWithParent.Value)
					{
						if (statementStart != rootStart)
						{
							yield return BaseStyleValidator.Report(statementClause,
								$"��������� ������ ����� ����� �� ������, ��� � �������� ({GetNodePosition(rootStatementSyntax)}).");
							continue;
						}
					}
					else
					{
						if (IsAllowedOneLineSyntaxToken(rootEndLine, statementLine, rootStatementSyntax, statementClause))
							continue;
						var report = ValidateIndent(rootStatementSyntax, statementStart, rootStart, statementLine, rootLine);
						if (report != null)
						{
							yield return BaseStyleValidator.Report(statementClause, report);
							continue;
						}
					}
				}
				foreach (var nestedError in CheckNonBracesStatements(statementClause))
					yield return nestedError;
			}
		}

		private static bool IsAllowedOneLineSyntaxToken(
			int rootEndLine,
			int statementLine,
			SyntaxNodeOrToken rootStatementSyntax,
			SyntaxNodeOrToken statementClause)
		{
			return rootEndLine == statementLine &&
					(rootStatementSyntax.Kind == SyntaxKind.IfStatement ||
					rootStatementSyntax.Kind == SyntaxKind.ElseClause) &&
					statementClause.Kind != SyntaxKind.IfStatement &&
					statementClause.Kind != SyntaxKind.ForStatement &&
					statementClause.Kind != SyntaxKind.ForEachStatement &&
					statementClause.Kind != SyntaxKind.WhileStatement &&
					statementClause.Kind != SyntaxKind.DoStatement;
		}

		private static string ValidateIndent(
			SyntaxNodeOrToken root,
			int statementStart,
			int rootStart,
			int statementLine,
			int rootLine)
		{
			if (statementLine == rootLine)
			{
				return "��������� ������ ����� �������������� ������� ������ " +
						$"����� �������� ({GetNodePosition(root)}).";
			}
			if (statementStart <= rootStart)
			{
				return "��������� ������ ����� ������ ������, " +
						$"��� � �������� ({GetNodePosition(root)}).";
			}
			var delta = statementStart - rootStart;
			if (delta < 4)
			{
				return "��������� ������ ����� ������, �� ������ 4 �������� " +
						$"������������ �������� ({GetNodePosition(root)}).";
			}
			return null;
		}

		private static bool NeedToValidateNonBracesTokens(SyntaxNode syntaxNode)
		{
			var syntaxKind = syntaxNode.Kind();
			return syntaxKind == SyntaxKind.IfStatement
					|| syntaxKind == SyntaxKind.WhileStatement
					|| syntaxKind == SyntaxKind.ForStatement
					|| syntaxKind == SyntaxKind.ForEachStatement
					|| syntaxKind == SyntaxKind.DoStatement;
		}

		private static string GetNodePosition(SyntaxNodeOrToken nodeOrToken)
		{
			var linePosition = nodeOrToken.GetFileLinePositionSpan().StartLinePosition;
			return $"c����� {linePosition.Line + 1}, ������� {linePosition.Character}";
		}
	}
}