using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Web.Mvc;
using Database.DataContexts;
using Database.Models;
using Kontur.Spam.Client;
using log4net;
using Microsoft.AspNet.Identity;

namespace uLearn.Web.Controllers
{
	public class UserControllerBase : Controller
	{
		private static readonly ILog log = LogManager.GetLogger(typeof(UserControllerBase));

		protected readonly ULearnDb db;
		protected UserManager<ApplicationUser> userManager;
		protected readonly UsersRepo usersRepo;
		protected readonly string secretForHashes;

		protected string spamChannelId;
		protected SpamClient spamClient;
		protected string spamTemplateId;

		protected UserControllerBase(ULearnDb db)
		{
			this.db = db;
			userManager = new ULearnUserManager(db);
			usersRepo = new UsersRepo(db);

			secretForHashes = WebConfigurationManager.AppSettings["ulearn.secretForHashes"] ?? "";

			var spamEndpoint = WebConfigurationManager.AppSettings["ulearn.spam.endpoint"] ?? "";
			var spamLogin = WebConfigurationManager.AppSettings["ulearn.spam.login"] ?? "ulearn";
			var spamPassword = WebConfigurationManager.AppSettings["ulearn.spam.password"] ?? "";
			spamChannelId = WebConfigurationManager.AppSettings["ulearn.spam.channels.emailConfirmations"] ?? "";
			spamTemplateId = WebConfigurationManager.AppSettings["ulearn.spam.templates.withButton"] ?? "";

			try
			{
				spamClient = new SpamClient(new Uri(spamEndpoint), spamLogin, spamPassword);
			}
			catch (Exception e)
			{
				log.Error($"Can\'t initialize Spam.API client to {spamEndpoint}, login {spamLogin}, password {spamPassword.MaskAsSecret()}", e);
				throw;
			}
		}

		protected UserControllerBase() : this(new ULearnDb())
		{
		}

		protected string GetEmailConfirmationSignature(string email)
		{
			return $"{secretForHashes}email={email}{secretForHashes}".CalculateMd5();
		}

		protected async Task<bool> SendConfirmationEmail(ApplicationUser user)
		{
			var confirmationUrl = Url.Action("ConfirmEmail", "Account", new { email = user.Email, signature = GetEmailConfirmationSignature(user.Email) }, "https");
			var subject = "������������� ������";

			var messageInfo = new MessageSentInfo
			{
				RecipientAddress = user.Email,
				RecipientName = user.VisibleName,
				Subject = subject,
				TemplateId = spamTemplateId,
				Variables = new Dictionary<string, object>
				{
					{ "title", subject },
					{ "content", $"<h2>������, {user.VisibleName}!</h2><p>����������� ����� ����������� �����, ������� �� ������:</p>" },
					{ "text_content", $"������, {user.VisibleName}!\n����������� ����� ����������� �����, ������� �� ������:" },
					{ "button", true },
					{ "button_link", confirmationUrl },
					{ "button_text", "����������� �����" },
					{ "content_after_button",
						"<p>���������� �����, �� ������� ������������ ������ � ������ �������� " +
						"� ����� ������, � ����� �������� ����������� � ���, ��� ���������� " +
						"� ����� ������.</p><p>�� �� �������� ��� �� �� ����� ������������� ��������, " +
						"� ��� ����������� ����� ��������� � ����� �������.</p><p>" +
						"���� ������ ��� ������������� ����� �� ��������, ������ ���������� ����� " +
						$"� �������� ��� � �������� ������ ��������: <a href=\"{confirmationUrl}\">{confirmationUrl}</a></p>" }
				}
			};

			try
			{
				await spamClient.SentMessageAsync(spamChannelId, messageInfo);
			}
			catch (Exception e)
			{
				log.Error($"�� ���� ��������� ������ ��� ������������� ������ �� {user.Email}", e);
				return false;
			}

			await usersRepo.UpdateLastConfirmationEmailTime(user);
			return true;
		}
	}
}