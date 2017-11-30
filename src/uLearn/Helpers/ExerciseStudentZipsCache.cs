﻿using System;
using System.Configuration;
using System.IO;
using log4net;
using uLearn.Extensions;

namespace uLearn.Helpers
{
	public class ExerciseStudentZipsCache
	{
		public bool IsEnabled { get; private set; }

		private static ILog log = LogManager.GetLogger(typeof(ExerciseStudentZipsCache)); 
		
		private readonly DirectoryInfo cacheDirectory;
		private readonly ExerciseStudentZipBuilder builder;

		public ExerciseStudentZipsCache()
		{
			IsEnabled = Convert.ToBoolean(ConfigurationManager.AppSettings["ulearn.buildExerciseStudentZips"] ?? "false");

			if (IsEnabled)
			{
				cacheDirectory = GetCacheDirectory();
				cacheDirectory.EnsureExists();
				builder = new ExerciseStudentZipBuilder();
			}
		}

		private static DirectoryInfo GetCacheDirectory()
		{
			var directory = ConfigurationManager.AppSettings["ulearn.exerciseStudentZipsDirectory"] ?? "";
			if (!string.IsNullOrEmpty(directory))
				return new DirectoryInfo(directory);

			return CourseManager.GetCoursesDirectory().GetSubdirectory("ExerciseStudentZips");
		}

		public FileInfo GenerateOrFindZip(string courseId, Slide slide)
		{
			if (! IsEnabled)
				throw new InvalidOperationException("ExerciseStudentZipsCache is disabled: can not generate zip archive with exercise");
			
			var courseDirectory = cacheDirectory.GetSubdirectory(courseId);
			var zipFile = courseDirectory.GetFile($"{slide.Id}.zip");
			if (!zipFile.Exists)
			{
				courseDirectory.EnsureExists();
				log.Info($"Собираю zip-архив с упражнением: курс {courseId}, слайд «{slide.Title}» ({slide.Id}), файл {zipFile.FullName}");
				builder.BuildStudentZip(slide, zipFile);
			}

			return zipFile;
		}

		public void DeleteCourseZips(string courseId)
		{
			if (! IsEnabled)
				throw new InvalidOperationException("ExerciseStudentZipsCache is disabled: can not delete course's zips");
			
			log.Info($"Очищаю папку со сгенерированными zip-архивами для упражнений из курса {courseId}");
			
			var courseDirectory = cacheDirectory.GetSubdirectory(courseId);
			courseDirectory.EnsureExists();

			FuncUtils.TrySeveralTimes(() => courseDirectory.ClearDirectory(), 3);
		}
	}
}