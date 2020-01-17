﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Database.Models;
using Ulearn.Common.Extensions;
using Ulearn.Core.Courses.Slides.Quizzes;
using Ulearn.Core.Courses.Slides.Quizzes.Blocks;

namespace Database.DataContexts
{
	public class UserQuizzesRepo
	{
		private readonly ULearnDb db;

		public UserQuizzesRepo(ULearnDb db)
		{
			this.db = db;
		}

		public UserQuizSubmission FindLastUserSubmission(string courseId, Guid slideId, string userId)
		{
			return db.UserQuizSubmissions.Where(s => s.CourseId == courseId && s.UserId == userId && s.SlideId == slideId).OrderByDescending(s => s.Timestamp).FirstOrDefault();
		}

		public async Task<UserQuizSubmission> AddSubmission(string courseId, Guid slideId, string userId, DateTime timestamp)
		{
			var submission = new UserQuizSubmission
			{
				CourseId = courseId,
				SlideId = slideId,
				UserId = userId,
				Timestamp = timestamp,
			};
			db.UserQuizSubmissions.Add(submission);
			await db.SaveChangesAsync().ConfigureAwait(false);
			return submission;
		}

		public async Task<UserQuizAnswer> AddUserQuizAnswer(int submissionId, bool isRightAnswer, string blockId, string itemId, string text, int quizBlockScore, int quizBlockMaxScore)
		{
			var answer = new UserQuizAnswer
			{
				SubmissionId = submissionId,
				IsRightAnswer = isRightAnswer,
				BlockId = blockId,
				ItemId = itemId,
				Text = text,
				QuizBlockScore = quizBlockScore,
				QuizBlockMaxScore = quizBlockMaxScore
			};
			db.UserQuizAnswers.Add(answer);
			await db.SaveChangesAsync().ConfigureAwait(false);
			return answer;
		}

		public bool IsWaitingForManualCheck(string courseId, Guid slideId, string userId)
		{
			return db.ManualQuizCheckings.Any(c => c.CourseId == courseId && c.SlideId == slideId && c.UserId == userId && !c.IsChecked);
		}

		public int GetUsedAttemptsCount(string courseId, string userId, Guid slideId)
		{
			return db.UserQuizSubmissions
				.Count(s => s.CourseId == courseId && s.UserId == userId && s.SlideId == slideId);
		}

		public HashSet<Guid> GetPassedSlideIds(string courseId, string userId)
		{
			return new HashSet<Guid>(db.UserQuizSubmissions.Where(x => x.CourseId == courseId && x.UserId == userId).Select(x => x.SlideId).Distinct());
		}

		public bool IsSlidePassed(string courseId, string userId, Guid slideId)
		{
			return db.UserQuizSubmissions.Any(x => x.CourseId == courseId && x.UserId == userId && x.SlideId == slideId);
		}

		public HashSet<Guid> GetPassedSlideIdsWithMaximumScore(string courseId, string userId)
		{
			var passedQuizzes = GetPassedSlideIds(courseId, userId);
			var notScoredMaximumSlides = db.UserQuizAnswers
				.Include(a => a.Submission)
				.Where(x => x.Submission.CourseId == courseId && x.Submission.UserId == userId && x.QuizBlockScore != x.QuizBlockMaxScore)
				.Select(x => x.Submission.SlideId)
				.Distinct();
			passedQuizzes.ExceptWith(notScoredMaximumSlides);
			return passedQuizzes;
		}

		public Dictionary<string, List<UserQuizAnswer>> GetAnswersForShowingOnSlide(string courseId, QuizSlide slide, string userId, UserQuizSubmission submission = null)
		{
			if (slide == null)
				return null;

			if (submission == null)
				submission = FindLastUserSubmission(courseId, slide.Id, userId);

			var answer = new Dictionary<string, List<UserQuizAnswer>>();
			foreach (var block in slide.Blocks.OfType<AbstractQuestionBlock>())
			{
				if (submission != null)
				{
					var ans = db.UserQuizAnswers
						.Where(q => q.SubmissionId == submission.Id && q.BlockId == block.Id)
						.OrderBy(x => x.Id)
						.ToList();

					answer[block.Id] = ans;
				}
				else
					answer[block.Id] = new List<UserQuizAnswer>();
			}

			return answer;
		}

		public Dictionary<string, int> GetUserScores(string courseId, Guid slideId, string userId, UserQuizSubmission submission = null)
		{
			if (submission == null)
			{
				submission = FindLastUserSubmission(courseId, slideId, userId);
				if (submission == null)
					return new Dictionary<string, int>();
			}

			return db.UserQuizAnswers
				.Where(q => q.SubmissionId == submission.Id)
				.ToList()
				.DistinctBy(q => q.BlockId)
				.ToDictionary(q => q.BlockId, q => q.QuizBlockScore);
		}

		public bool IsQuizScoredMaximum(string courseId, Guid slideId, string userId)
		{
			var submission = FindLastUserSubmission(courseId, slideId, userId);
			if (submission == null)
				return false;

			return db.UserQuizAnswers
				.Where(q => q.SubmissionId == submission.Id)
				.All(q => q.QuizBlockScore == q.QuizBlockMaxScore);
		}

		public async Task SetScoreForQuizBlock(int submissionId, string blockId, int score)
		{
			db.UserQuizAnswers
				.Where(q => q.SubmissionId == submissionId && q.BlockId == blockId)
				.ForEach(q => q.QuizBlockScore = score);
			await db.SaveChangesAsync().ConfigureAwait(false);
		}

		public async Task<Dictionary<string, Dictionary<string, int>>> GetAnswersFrequencyForChoiceBlocks(string courseId, Guid slideId, int blocksCount)
		{
			var answers = await db.UserQuizAnswers.Include(q => q.Submission)
				.Where(q => q.Submission.CourseId == courseId && q.Submission.SlideId == slideId)
				.OrderByDescending(q => q.Submission.Id)
				.Select(q => new { q.Submission.UserId, q.Submission.Timestamp, q.BlockId, q.ItemId})
				.Take(500 * blocksCount)
				.ToListAsync().ConfigureAwait(false);
			var tries = answers
				.GroupBy(q => q.BlockId)
				.ToDictionary(g => g.Key, g => g.Select(v => (v.UserId, v.Timestamp)).Distinct().Count());
			return answers
				.GroupBy(q => q.BlockId)
				.ToDictionary(
					g => g.Key, 
					g => g
						.GroupBy(q => q.ItemId)
						.ToDictionary(g2 => g2.Key, g2 => g2.Count().PercentsOf(tries[g.Key]))
					);
		}
	}
}