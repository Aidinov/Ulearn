using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AntiPlagiarism.Web.Database;
using AntiPlagiarism.Web.Database.Models;
using Database;
using Database.Models;
using Database.Repos;
using ManualUtils.AntiPlagiarism;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Ulearn.Core.Configuration;
using Ulearn.Core.Logging;

namespace ManualUtils
{
	internal class Program
	{
		public static async Task Main(string[] args)
		{
			var configuration = ApplicationConfiguration.Read<UlearnConfiguration>();
			LoggerSetup.Setup(configuration.HostLog, configuration.GraphiteServiceName);
			var optionsBuilder = new DbContextOptionsBuilder<UlearnDb>()
				.UseLazyLoadingProxies()
				.UseSqlServer(configuration.Database);
			var db = new UlearnDb(optionsBuilder.Options);
			var aOptionsBuilder = new DbContextOptionsBuilder<AntiPlagiarismDb>()
				.UseLazyLoadingProxies()
				.UseSqlServer(configuration.Database);
			var adb = new AntiPlagiarismDb(aOptionsBuilder.Options);

			//await FindExternalSolutionsPlagiarism.UploadSolutions();
			//await FindExternalSolutionsPlagiarism.GetRawResults();
			//await FindExternalSolutionsPlagiarism.PrepareResults();

			//Users.PrintCourseAdmins(db);
			//await ScoresUpdater.UpdateTests(db, "java-rtf");
			//GetMostSimilarSubmission(adb);
			//ParsePairWeightsFromLogs();
			//GetBlackAndWhiteLabels(db, adb);
			//ParseTaskWeightsFromLogs();
			//CampusRegistration();
			//GetIps(db);
			//FillAntiplagFields.FillClientSubmissionId(adb);
			//await XQueueRunAutomaticChecking(db);
		}

		private static async Task XQueueRunAutomaticChecking(UlearnDb db)
		{
			var userSolutionsRepo = new UserSolutionsRepo(db, new TextsRepo(db), new WorkQueueRepo(db), null);
			var time = new DateTime(2021, 1, 30);
			var submissions = await db.UserExerciseSubmissions.Where(s =>
				s.AutomaticChecking.DisplayName == "XQueue watcher Stepik.org"
				&& (s.AutomaticChecking.Status == AutomaticExerciseCheckingStatus.Waiting || s.AutomaticChecking.Status == AutomaticExerciseCheckingStatus.RequestTimeLimit)
				&& s.AutomaticChecking.Timestamp > time)
				.OrderBy(c => c.Timestamp)
				.ToListAsync();
			var i = 0;
			foreach (var submission in submissions)
			{
				Console.WriteLine($"{i} from {submissions.Count} {submission.Id}");
				await userSolutionsRepo.RunAutomaticChecking(submission, TimeSpan.FromSeconds(25), false, -10);
				Thread.Sleep(1000);
				var result = await db.UserExerciseSubmissions
					.Include(s => s.AutomaticChecking)
					.Where(s => s.Id == submission.Id).FirstOrDefaultAsync();
				Console.WriteLine($"IsRightAnswer: {result.AutomaticCheckingIsRightAnswer}");
				Console.WriteLine($"Status: {result.AutomaticChecking.Status}");
				i++;
			}
		}

		private static void GetMostSimilarSubmission(AntiPlagiarismDb adb)
		{
			//var lines = File.ReadLines("pairweights.txt");
			//var jsons = AntiplagiarismLogsParser.GetWeightsOfSubmissionPairs(lines).Select(JsonConvert.SerializeObject);
			//File.WriteAllLines("result.txt", jsons);
			var bestPairWeight = File.ReadLines("result.txt").Select(JsonConvert.DeserializeObject<BestPairWeight>);
			var now = DateTime.UtcNow;
			var mostSimilarSubmissions = bestPairWeight.Select(s => new MostSimilarSubmission
			{
				SubmissionId = s.Submission,
				SimilarSubmissionId = s.Other,
				Weight = s.Weight,
				Timestamp = now
			}).ToList();

			var exist = adb.MostSimilarSubmissions.Select(s => s.SubmissionId).ToList().ToHashSet();
			var i = 0;
			foreach (var mostSimilarSubmission in mostSimilarSubmissions)
			{
				if(exist.Contains(mostSimilarSubmission.SubmissionId))
					continue;
				adb.MostSimilarSubmissions.Add(mostSimilarSubmission);
				if (i % 1000 == 0)
				{
					adb.SaveChanges();
					Console.WriteLine(i);
				}
				i++;
			}
			adb.SaveChanges();
		}

		private static void GetBlackAndWhiteLabels(UlearnDb db, AntiPlagiarismDb adb)
		{
			var lines = File.ReadLines("pairweights.txt");
			var jsons = PlagiarismInstructorDecisions.GetBlackAndWhiteLabels(db, adb,
				lines.Select(JsonConvert.DeserializeObject<BestPairWeight>));
			File.WriteAllLines("blackAndWhiteLabels.txt", jsons);
		}

		private static void ParsePairWeightsFromLogs()
		{
			var lines = File.ReadLines("pairweights.txt");
			var jsons = AntiplagiarismLogsParser.GetWeightsOfSubmissionPairs(lines).Select(JsonConvert.SerializeObject);
			File.WriteAllLines("result.txt", jsons);
		}

		private static void ParseTaskWeightsFromLogs(UlearnDb db)
		{
			var lines = File.ReadLines("weights.txt");
			var jsons = AntiplagiarismLogsParser.GetWeightsForStatistics(db, lines);
			File.WriteAllLines("result.txt", jsons);
		}

		private static void CampusRegistration(UlearnDb db)
		{
			ManualUtils.CampusRegistration.Run(db, courseId: "Campus1920", groupId: 803, slideWithRegistrationQuiz: new Guid("67bf45bd-bebc-4bde-a705-c16763b94678"), false);
		}

		private static void GetIps(UlearnDb db)
		{
			// Для получения городов см. geoip.py
			// Где взять GeoLite2-City.mmdb читай в GeoLite2-City.mmdb.readme.txt
			var courses = new[] { "BasicProgramming", "BasicProgramming2", "Linq", "complexity", "CS2" };
			GetIpAddresses.Run(db, lastMonthCount: 13, courses, isNotMembersOfGroups: true, onlyRegisteredFrom: true);
		}
	}
}