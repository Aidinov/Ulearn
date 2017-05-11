﻿using System;
using System.Collections.Generic;
using System.Linq;
using uLearn.CSharp;

namespace uLearn.Courses.Linq.Slides.LINQ.LINQ.LINQ
{
	[Slide("Объединение коллекций", "{7DB3F797-B99B-4580-ABE6-BB4EE929BB6B}")]
	public class S050_SelectManyExercise : SlideTestBase
	{
		/*

		Вам дан список всех классов в школе. Нужно получить список всех учащихся всех классов.
		
		Учебный класс определен так:
		*/

		public class Classroom
		{
			public List<string> Students = new List<string>();
		}

		/*

		Без использования `LINQ` решение могло бы выглядеть так:
		*/

		[ShowBodyOnSlide]
		public string[] GetAllStudents_NoLinq(Classroom[] classes)
		{
			var allStudents = new List<string>();
			foreach (var classroom in classes)
			{
				foreach (var student in classroom.Students)
				{
					allStudents.Add(student);
				}
			}
			return allStudents.ToArray();
		}

		/*
		Напишите решение этой задачи с помощью `LINQ` в одно выражение.
		*/

		[ExpectedOutput("Alex Anna Bulat Galina Ilya Ivan Pavel Petr Vladimir")]
		public static void Main()
		{
			Classroom[] classes =
			{
				new Classroom {Students = {"Pavel", "Ivan", "Petr"},},
				new Classroom {Students = {"Anna", "Ilya", "Vladimir"},},
				new Classroom {Students = {"Bulat", "Alex", "Galina"},}
			};
			var allStudents = GetAllStudents(classes);
			Array.Sort(allStudents);
			Console.WriteLine(string.Join(" ", allStudents));
		}

		[Exercise]
		[SingleStatementMethod]
		[Hint("`IEnumerable<R> SelectMany(this IEnumerable<T> items, Func<T, IEnumerable<R>> f)`")]
		[Hint("`T[] ToArray(this IEnumerable<T> items)`")]
		public static string[] GetAllStudents(Classroom[] classes)
		{
			return classes.SelectMany(c => c.Students).ToArray();
		}
	}
}