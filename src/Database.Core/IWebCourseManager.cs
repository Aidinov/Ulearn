﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Ulearn.Core.Courses;

namespace Database
{
	public interface IWebCourseManager
	{
		Task<Course> GetCourseAsync(string courseId);
		Task<IEnumerable<Course>> GetCoursesAsync();
		void UpdateCourseVersion(string courseId, Guid versionId);
		IEnumerable<Course> GetCourses();
		Course GetCourse(string courseId);
		Task<Course> FindCourseAsync(string courseId);
		Course FindCourse(string courseId);
		bool HasCourse(string courseId);
		Task<bool> HasCourseAsync(string courseId);
		Course GetVersion(Guid versionId);
		FileInfo GetStagingCourseFile(string courseId);
		FileInfo GetStagingTempCourseFile(string courseId);
		DirectoryInfo GetExtractedCourseDirectory(string courseId);
		DirectoryInfo GetExtractedVersionDirectory(Guid versionId);
		FileInfo GetCourseVersionFile(Guid versionId);
		string GetStagingCoursePath(string courseId);
		Course LoadCourseFromDirectory(DirectoryInfo dir);
		string GetPackageName(string courseId);
		string GetPackageName(Guid versionId);
		DateTime GetLastWriteTime(string courseId);
		bool TryCreateCourse(string courseId, string courseTitle, Guid firstVersionId);
		void EnsureVersionIsExtracted(Guid versionId);
		bool HasPackageFor(string courseId);
		Course FindCourseBySlideById(Guid slideId);
		void WaitWhileCourseIsLocked(string courseId);
		void MoveCourse(Course course, DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory);
		void ReloadCourse(string courseId);
		Course ReloadCourseFromDirectory(DirectoryInfo directory);
		void ExtractTempCourseChanges(string tempCourseId);
		bool TryCreateTempCourse(string courseId, string courseTitle, Guid firstVersionId);
	}
}