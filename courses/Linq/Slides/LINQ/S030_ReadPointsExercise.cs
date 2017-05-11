﻿using System;
using System.Collections.Generic;
using System.Linq;
using uLearn.CSharp;

namespace uLearn.Courses.Linq.Slides.LINQ.LINQ.LINQ
{
	[Slide("Чтение списка точек", "{563307C9-F265-4EA0-B06E-8390582F718E}")]
	public class S030_ReadPointsExercise : SlideTestBase
	{
		/*

		В файле в каждой строке написаны две координаты точки (X, Y), разделенные пробелом.
		Кто-то уже вызвал метод `File.ReadLines(filename)` и теперь у вас есть массив всех строк файла.
		*/

		[ExpectedOutput("1 -2\n-3 4\n0 2\n1 -42")]
		public static void Main()
		{
			// Функция тестирования ParsePoints

			foreach (var point in ParsePoints(new[] { "1 -2", "-3 4", "0 2" }))
				Console.WriteLine(point.X + " " + point.Y);
			foreach (var point in ParsePoints(new List<string> { "+01 -0042" }))
				Console.WriteLine(point.X + " " + point.Y);
		}

		public class Point
		{
			public Point(int x, int y)
			{
				X = x;
				Y = y;
			}
			public int X, Y;
		}

		/*
		Реализуйте метод `ParsePoints` в одно `LINQ`-выражение.
		
		Постарайтесь не использовать функцию преобразования строки в число более одного раза.
		*/

		[Exercise]
		[SingleStatementMethod]
		[Hint("string.Split — разбивает строку на части по разделителю")]
		[Hint("int.Parse преобразует строку в целое число.")]
		[Hint(@"Каждую строку нужно преобразовать в точку. Преобразование — это дело для метода Select. 
			Но каждая строка — это список координат, каждую из которых нужно преобразовать из строки в число.
			Подумайте про Select внутри Select-а.")]
		public static List<Point> ParsePoints(IEnumerable<string> lines)
		{
			return lines
				.Select(line => line.Split(' ').Select(int.Parse).ToList())
				.Select(nums => new Point(nums[0], nums[1]))
				.ToList();
			/*uncomment
			return lines
				.Select(...)
				...
			*/
		}
	}
}