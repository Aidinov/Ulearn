﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Mvc;
using Database.DataContexts;
using Database.Models;
using Exceptions;
using log4net;
using Microsoft.AspNet.Identity;
using SendGrid;
using uLearn.Web.Models;

namespace uLearn.Web.Controllers
{
	public class RestorePasswordController : Controller
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(RestorePasswordController));
		private readonly RestoreRequestRepo requestRepo;
		private readonly UserManager<ApplicationUser> userManager;
		private readonly ULearnDb db = new ULearnDb();

		public RestorePasswordController()
		{
			userManager = new ULearnUserManager(db);
			requestRepo = new RestoreRequestRepo(db);
		}

		public ActionResult Index()
		{
			return View(new RestorePasswordModel());
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> Index(string username)
		{
			var users = await FindUsers(username);
			var answer = new RestorePasswordModel
			{
				UserName = username
			};

			if (!users.Any())
			{
				answer.Messages.Add(new Message($"Пользователь {username} не найден"));
				return View(answer);
			}

			foreach (var user in users)
			{
				if (string.IsNullOrWhiteSpace(user.Email))
				{
					answer.Messages.Add(new Message($"У пользователя {user.UserName} не указан email"));
					continue;
				}

				var requestId = await requestRepo.CreateRequest(user.Id);

				if (requestId == null)
				{
					answer.Messages.Add(new Message($"Слишком частые запросы для пользователя {user.UserName}. Попробуйте ещё раз через несколько минут"));
					continue;
				}

				await SendRestorePasswordEmail(requestId, user);
				answer.Messages.Add(new Message($"Письмо с инструкцией по восстановлению пароля для пользователя {user.UserName} отправлено на Ваш email", false));
			}

			return View(answer);
		}

		private async Task<List<ApplicationUser>> FindUsers(string info)
		{
			var user = await userManager.FindByNameAsync(info);
			if (user != null)
				return new List<ApplicationUser> { user };
			return db.Users.Where(appUser => appUser.Email == info).ToList();
		}

		private async Task SendRestorePasswordEmail(string requestId, ApplicationUser user)
		{
			var url = Url.Action("SetNewPassword", "RestorePassword", new { requestId }, "https");

			var message = new SendGridMessage();
			message.AddTo(user.Email);
			message.From = new MailAddress("noreply@ulearn.azurewebsites.net", "Добрый робот uLearn");
			message.Subject = "Восстановление пароля uLearn";
			message.Html = "Чтобы изменить пароль к аккаунту " + user.UserName + ", перейдите по ссылке: <a href=\"" + url + "\">" + url + "</a>";

			var login = ConfigurationManager.AppSettings["SendGrid.Login"];
			var password = ConfigurationManager.AppSettings["SendGrid.Password"];
			var credentials = new NetworkCredential(login, password);

			var transport = new SendGrid.Web(credentials);

			try
			{
				await transport.DeliverAsync(message);
			}
			catch (InvalidApiRequestException ex)
			{
				log.Error("Произошла ошибка при отправке письма", ex);
				throw new Exception(ex.Message + ":\n\n" + string.Join("\n", ex.Errors), ex);
			}
		}

		public ActionResult SetNewPassword(string requestId)
		{
			if (!requestRepo.ContainsRequest(requestId))
				return View(new SetNewPasswordModel
				{
					Errors = new[] { "Запрос не найден" }
				});

			return View(new SetNewPasswordModel
			{
				RequestId = requestId,
				Errors = new string[0]
			});
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<ActionResult> SetNewPassword(SetNewPasswordModel model)
		{
			var answer = new SetNewPasswordModel
			{
				RequestId = model.RequestId
			};
			if (!ModelState.IsValid)
			{
				answer.Errors = ModelState.Values.SelectMany(state => state.Errors.Select(error => error.ErrorMessage)).ToArray();
				return View(answer);
			}

			var userId = requestRepo.FindUserId(model.RequestId);
			if (userId == null)
			{
				answer.Errors = new[] { "Запрос не найден" };
				answer.RequestId = null;
				return View(answer);
			}

			var result = await userManager.RemovePasswordAsync(userId);
			if (!result.Succeeded)
			{
				answer.Errors = result.Errors.ToArray();
				return View(answer);
			}

			result = await userManager.AddPasswordAsync(userId, model.NewPassword);
			if (!result.Succeeded)
			{
				answer.Errors = result.Errors.ToArray();
				return View(answer);
			}

			await requestRepo.DeleteRequest(model.RequestId);

			var user = await userManager.FindByIdAsync(userId);
			await AuthenticationManager.LoginAsync(HttpContext, user, false);

			return RedirectToAction("Index", "Home");
		}
	}
}