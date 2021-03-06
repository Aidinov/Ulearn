using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Serialization;
using Ulearn.Core.Courses.Slides.Blocks;
using Ulearn.Core.Extensions;

namespace Ulearn.Core.Courses.Slides.Flashcards
{
	[XmlRoot("slide.flashcards", IsNullable = false, Namespace = "https://ulearn.me/schema/v2")]
	public class FlashcardSlide : Slide
	{
		[XmlElement(ElementName = "flashcard")]
		public Flashcard[] FlashcardsList { get; set; }

		public override void BuildUp(SlideLoadingContext context)
		{
			if (FlashcardsList is null)
				FlashcardsList = new Flashcard[0];
			foreach (var flashcard in FlashcardsList)
			{
				flashcard.BuildUp(context, this);
			}

			if (Title is null)
			{
				Title = "Вопросы для самопроверки";
			}

			CheckBlockTypes();

			base.BuildUp(context);
		}

		public override void Validate(SlideLoadingContext context)
		{
			if (Hide)
			{
				throw new CourseLoadingException(
					"Слайд с флэшкартами не может быть скрытым\n" +
					$"{Title}");
			}

			foreach (var flashcard in FlashcardsList)
			{
				flashcard.Validate(context, this);
			}

			var emptyIdFlashcards = FlashcardsList.Where(x => string.IsNullOrEmpty(x.Id)).ToList();
			if (emptyIdFlashcards.Any())
			{
				throw new CourseLoadingException(
					"Идентификаторы флеш-карт должны быть заполненными.\n" +
					"Модуль с флешкартами с пустыми идентификаторами:\n" +
					$"{Title}");
			}

			base.Validate(context);
		}

		[XmlIgnore]
		protected override Type[] AllowedBlockTypes { get; } =
		{
			typeof(MarkdownBlock),
			typeof(CodeBlock),
			typeof(TexBlock),
			typeof(HtmlBlock),
		};

		public new void CheckBlockTypes()
		{
			var blocks = FlashcardsList.SelectMany(x => x.Answer.Blocks).Concat(FlashcardsList.SelectMany(x => x.Question.Blocks));
			foreach (var block in blocks)
			{
				if (!AllowedBlockTypes.Any(type => type.IsInstanceOfType(block)))
					throw new CourseLoadingException(
						$"Недопустимый тип блока в слайде {Info.SlideFile.FullName}: <{block.GetType().GetXmlType()}>. " +
						$"В этом слайде разрешены только следующие блоки: {string.Join(", ", AllowedBlockTypes.Select(t => $"<{t.GetXmlType()}>"))}"
					);
			}
		}
	}
}