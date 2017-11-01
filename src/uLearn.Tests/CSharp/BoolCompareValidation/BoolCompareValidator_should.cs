﻿using System;
using System.IO;
using System.Linq;
using ApprovalTests.Reporters;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using uLearn.Extensions;

namespace uLearn.CSharp.BoolCompareValidation
{
	[TestFixture]
	public class BoolCompareValidator_should
	{
		private BoolCompareValidator validator;

		[SetUp]
		public void SetUp()
		{
			validator = new BoolCompareValidator();
		}

		private static readonly DirectoryInfo testDataDir = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..",
			"..", "CSharp", "BoolCompareValidation", "TestData"));

		private static DirectoryInfo IncorrectTestDataDir => testDataDir.GetDirectories("Incorrect").Single();
		private static DirectoryInfo CorrectTestDataDir => testDataDir.GetDirectories("Correct").Single();

		private static string[] CorrectFilenames => CorrectTestDataDir.GetFiles().Select(f => f.Name).ToArray();
		private static string[] IncorrectFilenames => IncorrectTestDataDir.GetFiles().Select(f => f.Name).ToArray();

		[Test]
		[TestCaseSource(nameof(IncorrectFilenames))]
		public void FindErrors(string filename)
		{
			var code = IncorrectTestDataDir.GetFiles(filename).Single().ContentAsUtf8();
			var binaryExpressionSyntaxsCount = CSharpSyntaxTree.ParseText(code).GetRoot().DescendantNodes().OfType<BinaryExpressionSyntax>().Count();
			var errors = validator.FindError(code);
			errors.Should().NotBeNullOrEmpty();
			errors.Split('\n').Length.Should().Be(binaryExpressionSyntaxsCount, errors);
		}

		[Test]
		[TestCaseSource(nameof(CorrectFilenames))]
		public void NotFindErrors(string filename)
		{
			var code = CorrectTestDataDir.GetFiles(filename).Single().ContentAsUtf8();
			var errors = validator.FindError(code);
			if (errors != null)
			{
				Console.WriteLine(errors);
			}
			errors.Should().BeNullOrEmpty();
		}
	}
}