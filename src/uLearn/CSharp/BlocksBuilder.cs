using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using uLearn.Model.Blocks;

namespace uLearn.CSharp
{
	public class SlideBuilder : CSharpSyntaxRewriter
	{
		public const string LangId = "cs";
		private readonly IFileSystem fs;
		public readonly List<SlideBlock> Blocks = new List<SlideBlock>();
		public string Title;
		public Guid Id;

		public SlideBuilder(IFileSystem fs) : base(false)
		{
			this.fs = fs;
		}

		private SyntaxNode VisitMemberDeclaration(MemberDeclarationSyntax node, SyntaxNode newNode)
		{
			var parent = node.GetParents().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
			if (!ShowOnSlide(node)) return null;
			if (parent == null 
				|| parent.HasAttribute<SlideAttribute>() 
				|| parent.HasAttribute<ShowBodyOnSlideAttribute>())
				AddCodeBlock(((MemberDeclarationSyntax)newNode));
			return ((MemberDeclarationSyntax)newNode).WithoutAttributes();
		}

		public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
		{
			if (node.HasAttribute<SlideAttribute>())
			{
				var arguments =
					node.GetAttributes<SlideAttribute>()
					.Select(a => new { title = a.GetArgument(0), id = a.GetArgument(1) })
					.Single();
				Title = arguments.title;
				Id = Guid.Parse(arguments.id);
			}
			return VisitMemberDeclaration(node, base.VisitClassDeclaration(node));
		}

		public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
		{
			return VisitMemberDeclaration(node, base.VisitInterfaceDeclaration(node));
		}

		public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
		{
			return VisitMemberDeclaration(node, base.VisitStructDeclaration(node));
		}

		public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
		{
			return VisitMemberDeclaration(node, base.VisitEnumDeclaration(node));
		}

		public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
		{
			return VisitMemberDeclaration(node, base.VisitConstructorDeclaration(node));
		}

		public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
		{
			return VisitMemberDeclaration(node, base.VisitFieldDeclaration(node));
		}

		public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
		{
			return VisitMemberDeclaration(node, base.VisitPropertyDeclaration(node));
		}

		public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
		{
			return VisitMemberDeclaration(node, base.VisitMethodDeclaration(node));
		}

		public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
		{
			var comment = trivia.ToString();
			if (trivia.Kind() == SyntaxKind.MultiLineCommentTrivia)
			{
				if (!comment.StartsWith("/*uncomment"))
				{
					bool shouldCreateTextBlock = trivia.GetParents().Count(p => IsNestingParent(p, trivia)) <= 1;
					if (shouldCreateTextBlock)
						Blocks.Add(ExtractMarkDownFromComment(trivia));
				}
			}
			else if (trivia.Kind() == SyntaxKind.SingleLineCommentTrivia)
			{
				if (comment.StartsWith("//#"))
				{
					var parts = comment.Split(new[] { ' ' }, 2);
					if (parts[0] == "//#video") EmbedVideo(parts[1]);
                    if (parts[0] == "//#include") EmbedCode(parts[1]);
                    if (parts[0] == "//#para") EmbedPara(parts[1]);
                    if (parts[0] == "//#gallery") EmbedGallery(parts[1]);
				}
			}
			return base.VisitTrivia(trivia);
		}

	    private void EmbedPara(string filename)
	    {
	        Blocks.Add(new MdBlock(fs.GetContent(filename)));
//			Blocks.Add(new IncludeMdBlock(filename));
	    }

	    private void EmbedGallery(string folderName)
		{
			string[] images = fs.GetFilenames(folderName);
			Blocks.Add(new ImageGaleryBlock(images));
//			Blocks.Add(new IncludeImageGalleryBlock(folderName));
		}

		///<summary>Is child _inside_ Type or Method parent</summary>
		private bool IsNestingParent(SyntaxNode parent, SyntaxTrivia child)
		{
			return IsNestingParent(parent as TypeDeclarationSyntax, child) 
				|| IsNestingParent(parent as MethodDeclarationSyntax, child);
		}

		private bool IsNestingParent(TypeDeclarationSyntax node, SyntaxTrivia trivia)
		{
			if (node == null) return false;
			if (trivia.Span.Start < node.OpenBraceToken.Span.Start) return false;
			if (trivia.Span.End > node.CloseBraceToken.Span.End) return false;
			return true;
		}
		
		private bool IsNestingParent(MethodDeclarationSyntax node, SyntaxTrivia trivia)
		{
			if (node == null) return false;
			if (trivia.Span.Start < node.Body.Span.Start) return false;
			if (trivia.Span.End > node.Body.Span.End) return false;
			return true;
		}

		private void AddCodeBlock(MemberDeclarationSyntax node)
		{
			var code = (string)CreateCodeBlock((dynamic)node);
			var lastBlock = Blocks.LastOrDefault() as CodeBlock;
			if (lastBlock != null)
				Blocks[Blocks.Count - 1] = new CodeBlock(lastBlock.Code + "\r\n\r\n" + code, LangId);
			else
				Blocks.Add(new CodeBlock(code, LangId));
		}

		private string CreateCodeBlock(MethodDeclarationSyntax node)
		{
			return node.HasAttribute<ShowBodyOnSlideAttribute>()
				? node.Body.Statements.ToFullString().RemoveCommonNesting()
				: node.WithoutAttributes().ToPrettyString();
		}

		private string CreateCodeBlock(MemberDeclarationSyntax node)
		{
			return node.WithoutAttributes().ToPrettyString();
		}
		
		private static bool ShowOnSlide(MemberDeclarationSyntax node)
		{
			return !node.HasAttribute<SlideAttribute>()
			&& !node.HasAttribute<HideOnSlideAttribute>() 
			&& !node.HasAttribute<ExerciseAttribute>()
			&& !(node is TypeDeclarationSyntax && node.HasAttribute<ShowBodyOnSlideAttribute>());
		}

		private void EmbedCode(string filename)
		{
			Blocks.Add(new CodeBlock(fs.GetContent(filename), LangId));
//			Blocks.Add(new IncludeCodeBlock(filename));
		}

		private void EmbedVideo(string videoId)
		{
			Blocks.Add(new YoutubeBlock(videoId));
		}

		public static SlideBlock ExtractMarkDownFromComment(SyntaxTrivia comment)
		{
			int identation = comment.SyntaxTree.GetLineSpan(comment.FullSpan).StartLinePosition.Character;
			string[] commentLines = comment.ToString().SplitToLines();
			if (commentLines.First().EndsWith("tex"))
				return new TexBlock(commentLines.Skip(1).Take(commentLines.Length - 2).ToArray());
			return new MdBlock(GetCommentContent(commentLines, identation));
		}

		private static string GetCommentContent(string[] commentLines, int identation)
		{
			var sb = new StringBuilder();
			foreach (string line in commentLines.Skip(1).Take(commentLines.Length - 2))
			{
				if (line.Trim() != "")
				{
					if (line.Length < identation || line.Substring(0, identation).Trim() != "")
						throw new Exception("Wrong indentation in line: " + line);
					sb.AppendLine(line.Substring(identation));
				}
				else
				{
					sb.AppendLine();
				}
			}
			return sb.ToString();
		}
	}
}