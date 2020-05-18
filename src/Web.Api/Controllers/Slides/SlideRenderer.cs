﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Ulearn.Core;
using Ulearn.Core.Courses.Slides;
using Ulearn.Core.Courses.Slides.Blocks;
using Ulearn.Core.Courses.Slides.Exercises;
using Ulearn.Core.Courses.Slides.Flashcards;
using Ulearn.Core.Courses.Slides.Quizzes;
using Ulearn.Core.Courses.Slides.Quizzes.Blocks;
using Ulearn.Web.Api.Clients;
using Ulearn.Web.Api.Models.Common;
using Ulearn.Web.Api.Models.Responses.SlideBlocks;

namespace Ulearn.Web.Api.Controllers.Slides
{
	public class SlideRenderer
	{
		private readonly ILogger logger;
		private readonly IUlearnVideoAnnotationsClient videoAnnotationsClient;

		public SlideRenderer(ILogger logger, IUlearnVideoAnnotationsClient videoAnnotationsClient)
		{
			this.logger = logger;
			this.videoAnnotationsClient = videoAnnotationsClient;
		}

		public ShortSlideInfo BuildShortSlideInfo(string courseId, Slide slide, Func<Slide, int> getSlideMaxScoreFunc, IUrlHelper urlHelper)
		{
			return BuildShortSlideInfo<ShortSlideInfo>(courseId, slide, getSlideMaxScoreFunc, urlHelper);
		}

		private T BuildShortSlideInfo<T>(string courseId, Slide slide, Func<Slide, int> getSlideMaxScoreFunc, IUrlHelper urlHelper)
			where T : ShortSlideInfo, new()
		{
			return new T
			{
				Id = slide.Id,
				Title = slide.Title,
				Slug = slide.Url,
				ApiUrl = urlHelper.Action("SlideInfo", "Slides", new { courseId = courseId, slideId = slide.Id }),
				MaxScore = getSlideMaxScoreFunc(slide),
				ScoringGroup = slide.ScoringGroup,
				Type = GetSlideType(slide),
				QuestionsCount = slide.Blocks.OfType<AbstractQuestionBlock>().Count(),

				// TODO: кол-во попыток
			};
		}

		private static SlideType GetSlideType(Slide slide)
		{
			switch (slide)
			{
				case ExerciseSlide _:
					return SlideType.Exercise;
				case QuizSlide _:
					return SlideType.Quiz;
				case FlashcardSlide _:
					return SlideType.Flashcards;
				default:
					return SlideType.Lesson;
			}
		}

		public async Task<ApiSlideInfo> BuildSlideInfo(SlideRenderContext slideRenderContext, Func<Slide, int> getSlideMaxScoreFunc)
		{
			var result = BuildShortSlideInfo<ApiSlideInfo>(slideRenderContext.CourseId, slideRenderContext.Slide, getSlideMaxScoreFunc, slideRenderContext.UrlHelper);
			result.Blocks = new List<IApiSlideBlock>();
			foreach (var b in slideRenderContext.Slide.Blocks)
				result.Blocks.AddRange(await ToApiSlideBlocks(b, slideRenderContext));
			return result;
		}

		public async Task<IEnumerable<IApiSlideBlock>> ToApiSlideBlocks(SlideBlock slideBlock, SlideRenderContext context)
		{
			if (context.RemoveHiddenBlocks && slideBlock.Hide)
				return new IApiSlideBlock[] {};
			var apiSlideBlocks = (IEnumerable<IApiSlideBlock>)await RenderBlock((dynamic)slideBlock, context);
			if (context.RemoveHiddenBlocks)
				apiSlideBlocks = apiSlideBlocks.Where(b => !b.Hide);
			return apiSlideBlocks;
		}
		
		private async Task<IEnumerable<IApiSlideBlock>> RenderBlock(SlideBlock b, SlideRenderContext context)
		{
			return Enumerable.Empty<IApiSlideBlock>();
		}

		private async Task<IEnumerable<IApiSlideBlock>> RenderBlock(CodeBlock b, SlideRenderContext context)
		{
			return new[] { new CodeBlockResponse(b) };
		}
		
		private async Task<IEnumerable<IApiSlideBlock>> RenderBlock(HtmlBlock b, SlideRenderContext context)
		{
			return new[] { new HtmlBlockResponse(b, false) };
		}
		
		private async Task<IEnumerable<IApiSlideBlock>> RenderBlock(ImageGalleryBlock b, SlideRenderContext context)
		{
			return new[] { new ImageGalleryBlockResponse(b) };
		}
		
		private async Task<IEnumerable<IApiSlideBlock>> RenderBlock(TexBlock b, SlideRenderContext context)
		{
			return new[] { new TexBlockResponse(b) };
		}

		private async Task<IEnumerable<IApiSlideBlock>> RenderBlock(SpoilerBlock sb, SlideRenderContext context)
		{
			var innerBlocks = new List<IApiSlideBlock>();
			foreach (var b in sb.Blocks)
				innerBlocks.AddRange(await ToApiSlideBlocks(b, context));
			if (sb.Hide)
				innerBlocks.ForEach(b => b.Hide = true);
			return new [] { new SpoilerBlockResponse(sb, innerBlocks) };
		}

		private static async Task<IEnumerable<IApiSlideBlock>> RenderBlock(MarkdownBlock mb, SlideRenderContext context)
		{
			var renderedMarkdown = mb.RenderMarkdown(context.CourseId, context.Slide.Id, context.BaseUrl);
			var parsedBlocks = ParseBlocksFromMarkdown(renderedMarkdown);
			if (mb.Hide)
				parsedBlocks.ForEach(b => b.Hide = true);
			return parsedBlocks;
		}

		private async Task<IEnumerable<IApiSlideBlock>> RenderBlock(YoutubeBlock yb, SlideRenderContext context)
		{
			var annotation = await videoAnnotationsClient.GetVideoAnnotations(context.VideoAnnotationsGoogleDoc, yb.VideoId);
			var googleDocLink = string.IsNullOrEmpty(context.VideoAnnotationsGoogleDoc) ? null
				: "https://docs.google.com/document/d/" + context.VideoAnnotationsGoogleDoc;
			var response = new YoutubeBlockResponse(yb, annotation, googleDocLink);
			return new [] { response };
		}

		private static List<IApiSlideBlock> ParseBlocksFromMarkdown(string renderedMarkdown)
		{
			var parser = new HtmlParser();
			var document = parser.ParseDocument(renderedMarkdown);
			var rootElements = document.Body.Children;
			var blocks = new List<IApiSlideBlock>();
			foreach (var element in rootElements)
			{
				var tagName = element.TagName.ToLower();
				if (tagName == "textarea")
				{
					var langStr = element.GetAttribute("data-lang");
					var lang = (Language)Enum.Parse(typeof(Language), langStr, true);
					var code = element.TextContent;
					blocks.Add(new CodeBlockResponse { Code = code, Language = lang });
				}
				else if (tagName == "img")
				{
					var href = element.GetAttribute("href");
					blocks.Add(new ImageGalleryBlockResponse { ImageUrls = new[] { href } });
				}
				else if (tagName == "p"
						&& element.Children.Length == 1
						&& string.Equals(element.Children[0].TagName, "img", StringComparison.OrdinalIgnoreCase)
						&& string.IsNullOrWhiteSpace(element.TextContent))
				{
					var href = element.Children[0].GetAttribute("src");
					blocks.Add(new ImageGalleryBlockResponse { ImageUrls = new[] { href } });
				}
				else
				{
					var htmlContent = element.OuterHtml;
					if (blocks.Count > 0 && blocks.Last() is HtmlBlockResponse last)
					{
						htmlContent = last.Content + "\n" + htmlContent;
						blocks[blocks.Count - 1] = new HtmlBlockResponse { Content = htmlContent, FromMarkdown = true };
					}
					else
						blocks.Add(new HtmlBlockResponse { Content = htmlContent, FromMarkdown = true });
				}
			}
			return blocks;
		}
	}
}