﻿using System.Xml.Serialization;
using uLearn.Model.Blocks;

namespace uLearn.Model
{
	[XmlRoot("Lesson", IsNullable = false, Namespace = "https://ulearn.azurewebsites.net/lesson")]
	public class Lesson
	{
		[XmlElement("title")]
		public string Title;

		[XmlElement("id")]
		public string Id;
		
		[XmlElement("default-include-code-file")]
		public string DefaultIncludeCodeFile;

		[XmlElement(typeof(YoutubeBlock))]
		[XmlElement(typeof(MdBlock))]
		[XmlElement(typeof(CodeBlock))]
		[XmlElement(typeof(TexBlock))]
		[XmlElement(typeof(ImageGaleryBlock))]
		[XmlElement(typeof(IncludeCodeBlock))]
		[XmlElement(typeof(IncludeMdBlock))]
		[XmlElement(typeof(IncludeImageGalleryBlock))]
		[XmlElement(typeof(ExerciseBlock))]
		public SlideBlock[] Blocks;

		public Lesson()
		{
		}

		public Lesson(string title, string id, SlideBlock[] blocks)
		{
			Title = title;
			Id = id;
			Blocks = blocks;
		}
	}
}
