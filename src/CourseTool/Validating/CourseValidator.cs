using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using RunCsJob;
using uLearn.Model.Blocks;

namespace uLearn
{
	public class CourseValidator : BaseValidator
	{
		private readonly List<Slide> slides;
		private readonly SandboxRunnerSettings settings;

		public CourseValidator(List<Slide> slides, SandboxRunnerSettings settings)
		{
			this.slides = slides;
			this.settings = settings;
		}

		public void ValidateExercises() // todo ����������� log4net � ���� (������ ��������) � �� �������
		{
			foreach (var slide in slides.OfType<ExerciseSlide>())
			{
				LogSlideProcessing("Validate exercise", slide);

				if (slide.Exercise is ProjectExerciseBlock exercise)
				{
					new ProjectExerciseValidator(this, settings, slide, exercise).ValidateExercises();
				}
				else
					ReportIfEthalonSolutionHasErrorsOrIssues(slide);
			}
		}

		private void LogSlideProcessing(string prefix, Slide slide)
		{
			LogInfoMessage(prefix + " " + slide.Info.Unit.Title + " - " + slide.Title);
		}

		public void ValidateVideos()
		{
			var videos = GetVideos().ToLookup(d => d.Item2, d => d.Item1);
			foreach (var g in videos.Where(g => g.Count() > 1))
				ReportError("Duplicate videos on slides " + string.Join(", ", g));
			foreach (var g in videos)
			{
				var slide = g.First();
				LogSlideProcessing("Validate video", slide);
				var url = "https://www.youtube.com/oembed?format=json&url=http://www.youtube.com/watch?v=" + g.Key;
				try
				{
					new WebClient().DownloadData(url);
				}
				catch (Exception e)
				{
					ReportError("Slide " + slide + " contains not accessible video. " + e.Message);
				}
			}
		}

		public IEnumerable<Tuple<Slide, string>> GetVideos()
		{
			return slides
				.SelectMany(slide =>
					slide.Blocks.OfType<YoutubeBlock>()
						.Select(b => Tuple.Create(slide, b.VideoId)));
		}

		private void ReportIfEthalonSolutionHasErrorsOrIssues(ExerciseSlide slide)
		{
			var exercise = (SingleFileExerciseBlock)slide.Exercise;
			var ethalon = exercise.EthalonSolution.RemoveCommonNesting();
			var solution = exercise.BuildSolution(ethalon);
			if (solution.HasErrors)
			{
				FailOnError(slide, solution, ethalon);
				return;
			}
			if (solution.HasStyleIssues)
			{
				ReportWarning($"Slide {slide.Title} has style issues:\n{solution.StyleMessage}");
			}

			var result = SandboxRunner.Run(exercise.CreateSubmission(
				slide.Id.ToString(),
				ethalon), settings);

			var output = result.GetOutput().NormalizeEoln();

			var isRightAnswer = output.NormalizeEoln().Equals(slide.Exercise.ExpectedOutput.NormalizeEoln());
			if (!isRightAnswer)
			{
				ReportSlideError(slide,
					"Ethalon solution does not provide right answer\n" +
					"ActualOutput: " + output.NormalizeEoln() + "\n" +
					"ExpectedOutput: " + slide.Exercise.ExpectedOutput.NormalizeEoln() + "\n" +
					"CompilationError: " + result.CompilationOutput + "\n" +
					"SourceCode: " + solution.SourceCode + "\n\n");
			}
		}

		private void FailOnError(ExerciseSlide slide, SolutionBuildResult solution, string ethalonSolution)
		{
			ReportSlideError(slide, $@"ETHALON SOLUTION:
{ethalonSolution}
SOURCE CODE: 
{solution.SourceCode}
ERROR:
{solution.ErrorMessage}");
		}
	}
}