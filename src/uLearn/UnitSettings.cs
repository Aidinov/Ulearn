using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace uLearn
{
	[XmlRoot("Unit", IsNullable = false, Namespace = "https://ulearn.azurewebsites.net/unit")]
	public class UnitSettings
	{
		public UnitSettings()
		{
			Scoring = new ScoringSettings();
		}

		[XmlElement("id")]
		public Guid Id { get; set; }

		[XmlElement("url")]
		public string Url { get; set; }

		[XmlElement("title")]
		public string Title { get; set; }

		[XmlElement("scoring")]
		public ScoringSettings Scoring { get; set; }

		public static UnitSettings Load(FileInfo file, CourseSettings courseSettings)
		{
			var unitSettings = file.DeserializeXml<UnitSettings>();

			if (string.IsNullOrEmpty(unitSettings.Title))
				throw new CourseLoadingException($"��������� ������ �� ����� ���� ������. ���� {file.FullName}");

			if (string.IsNullOrEmpty(unitSettings.Url))
				unitSettings.Url = unitSettings.Title.ToLatin();
			
			var courseScoringGroupsIds = new HashSet<string>(courseSettings.Scoring.Groups.Keys);
			foreach (var scoringGroup in unitSettings.Scoring.Groups.Values.ToList())
			{
				if (!courseScoringGroupsIds.Contains(scoringGroup.Id))
					throw new CourseLoadingException(
						$"����������� ������ ������ ������� � ������: {scoringGroup.Id}. ���� {file.FullName}. " +
						$"��� ����� ���������� ������ ��������� ������ ������: {string.Join(", ", courseScoringGroupsIds)}"
						);

				/* By default set unit's scoring settings from course's scoring settings */
				var unitScoringGroup = unitSettings.Scoring.Groups[scoringGroup.Id];
				var courseScoringGroup = courseSettings.Scoring.Groups[scoringGroup.Id];
				unitScoringGroup.CopySettingsFrom(courseScoringGroup);

				if (scoringGroup.IsMaxAdditionalScoreSpecified &&
					(!scoringGroup.IsCanBeSetByInstructorSpecified || !scoringGroup.CanBeSetByInstructor))
					throw new CourseLoadingException(
						$"����� ���������� �������������� ����� � ������ {scoringGroup.Id}, ���������� � �� ������� set_by_instructor=\"true\" � ���������� ������ (���� Unit.xml) ��� ����� (���� Course.xml). " +
						$"� ��������� ������ ������� max_additional_score=\"{scoringGroup.MaxAdditionalScore}\" �� ���������"
						);
			}

			/* Copy other scoring groups and scoring settings from course settings */
			unitSettings.Scoring.CopySettingsFrom(courseSettings.Scoring);
			
			return unitSettings;
		}

		public static UnitSettings CreateByTitle(string title, CourseSettings courseSettings)
		{
			var unitSettings = new UnitSettings
			{
				Id = title.ToDeterministicGuid(),
				Url = title.ToLatin(),
				Title = title,
			};

			unitSettings.Scoring.CopySettingsFrom(courseSettings.Scoring);
			return unitSettings;
		}
	}
}