﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Database;
using Database.Models;
using Database.Repos;
using Database.Repos.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;
using Ulearn.Common.Extensions;
using Ulearn.Web.Api.Models.Parameters.Notifications;
using Ulearn.Web.Api.Models.Responses.Notifications;

namespace Ulearn.Web.Api.Controllers.Notifications
{
	[Route("/notifications")]
	public class NotificationsController : BaseController
	{
		private readonly IFeedRepo feedRepo;
		private readonly IServiceProvider serviceProvider;
		private readonly INotificationDataPreloader notificationDataPreloader;

		private static FeedNotificationTransport commentsFeedNotificationTransport;

		public NotificationsController(ILogger logger, IWebCourseManager courseManager, UlearnDb db,
			IUsersRepo usersRepo,
			IFeedRepo feedRepo,
			IServiceProvider serviceProvider,
			INotificationDataPreloader notificationDataPreloader)
			: base(logger, courseManager, db, usersRepo)
		{
			this.feedRepo = feedRepo;
			this.serviceProvider = serviceProvider;
			this.notificationDataPreloader = notificationDataPreloader;
		}

		public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
		{
			if (commentsFeedNotificationTransport == null)
				commentsFeedNotificationTransport = await feedRepo.GetCommentsFeedNotificationTransport();

			var userId = User.GetUserId();
			await feedRepo.AddFeedNotificationTransportIfNeeded(userId);

			await next();
		}

		/// <summary>
		/// Список уведомлений и комментариев
		/// </summary>
		[HttpGet]
		[Authorize]
		public async Task<ActionResult<NotificationListResponse>> NotificationList()
		{
			var userId = User.GetUserId();
			var (importantNotificationList, commentsNotificationList) = await GetNotificationListsAsync(userId);

			return new NotificationListResponse
			{
				Important = importantNotificationList,
				Comments = commentsNotificationList,
			};
		}

		/// <summary>
		/// Число непрочитанных уведомлений
		/// </summary>
		[HttpGet("count")]
		[Authorize]
		public async Task<ActionResult<NotificationsCountResponse>> NotificationsCount([FromQuery] NotificationsCountParameters parameters)
		{
			var userId = User.GetUserId();
			var userNotificationTransport = await feedRepo.GetUsersFeedNotificationTransport(userId);
			var unreadCountAndLastTimestamp = await GetUnreadNotificationsCountAndLastTimestampAsync(userId, userNotificationTransport, parameters.LastTimestamp);

			return new NotificationsCountResponse
			{
				Count = unreadCountAndLastTimestamp.Item1,
				LastTimestamp = unreadCountAndLastTimestamp.Item2,
			};
		}

		private async Task<Tuple<int, DateTime?>> GetUnreadNotificationsCountAndLastTimestampAsync(string userId, FeedNotificationTransport transport, DateTime? from = null)
		{
			var realFrom = from ?? await feedRepo.GetFeedViewTimestamp(userId, transport.Id) ?? DateTime.MinValue;
			var unreadCount = await feedRepo.GetNotificationsCount(userId, realFrom, transport);
			if (unreadCount > 0)
			{
				from = await feedRepo.GetLastDeliveryTimestamp(transport);
			}

			return Tuple.Create(unreadCount, from);
		}

		private async Task<(NotificationList, NotificationList)> GetNotificationListsAsync(string userId)
		{
			var notificationTransport = await feedRepo.GetUsersFeedNotificationTransport(userId);

			var importantNotifications = new List<Notification>();
			if (notificationTransport != null)
			{
				importantNotifications = await feedRepo.GetNotificationForFeedNotificationDeliveries(userId, n => n.InitiatedBy, transports: notificationTransport);
			}

			var commentsNotifications = await feedRepo.GetNotificationForFeedNotificationDeliveries(userId, n => n.InitiatedBy, transports: commentsFeedNotificationTransport);

			logger.Information($"[GetNotificationList] Step 1 done: found {importantNotifications.Count} important notifications and {commentsNotifications.Count} comment notifications");

			importantNotifications = RemoveBlockedNotifications(importantNotifications).ToList();
			commentsNotifications = RemoveBlockedNotifications(commentsNotifications, importantNotifications).ToList();

			logger.Information($"[GetNotificationList] Step 2 done, removed blocked notifications: left {importantNotifications.Count} important notifications and {commentsNotifications.Count} comment notifications");

			importantNotifications = RemoveNotActualNotifications(importantNotifications).ToList();
			commentsNotifications = RemoveNotActualNotifications(commentsNotifications).ToList();

			logger.Information($"[GetNotificationList] Step 3 done, removed not actual notifications: left {importantNotifications.Count} important notifications and {commentsNotifications.Count} comment notifications");

			var importantLastViewTimestamp = await feedRepo.GetFeedViewTimestamp(userId, notificationTransport?.Id ?? -1);
			var commentsLastViewTimestamp = await feedRepo.GetFeedViewTimestamp(userId, commentsFeedNotificationTransport.Id);

			logger.Information("[GetNotificationList] Step 4, building models");

			var allNotifications = importantNotifications.Concat(commentsNotifications).ToList();
			var notificationsData = await notificationDataPreloader.LoadAsync(allNotifications);

			var importantNotificationList = new NotificationList
			{
				LastViewTimestamp = importantLastViewTimestamp,
				Notifications = importantNotifications.Select(notification => BuildNotificationInfo(notification, notificationsData)).ToList(),
			};
			var commentsNotificationList = new NotificationList
			{
				LastViewTimestamp = commentsLastViewTimestamp,
				Notifications = commentsNotifications.Select(notification => BuildNotificationInfo(notification, notificationsData)).ToList(),
			};

			return (importantNotificationList, commentsNotificationList);
		}

		private IEnumerable<Notification> RemoveBlockedNotifications(IReadOnlyCollection<Notification> notifications, IReadOnlyCollection<Notification> searchBlockersAlsoIn = null)
		{
			var allNotifications = notifications.ToList();
			if (searchBlockersAlsoIn != null)
				allNotifications = allNotifications.Concat(searchBlockersAlsoIn).ToList();

			foreach (var notification in notifications)
			{
				if (notification.IsBlockedByAnyNotificationFrom(serviceProvider, allNotifications))
					continue;
				yield return notification;
			}
		}

		private IEnumerable<Notification> RemoveNotActualNotifications(IEnumerable<Notification> notifications)
		{
			return notifications.Where(notification =>
			{
				logger.Information($"Checking actuality of notification #{notification.Id}: {notification} ({notification.GetNotificationType().ToString()})");
				return notification.IsActual();
			});
		}

		private NotificationInfo BuildNotificationInfo(Notification notification, NotificationDataStorage notificationsData)
		{
			return new NotificationInfo
			{
				Id = notification.Id,
				Author = BuildShortUserInfo(notification.InitiatedBy),
				Type = notification.GetNotificationType().ToString(),
				CreateTime = notification.CreateTime,
				CourseId = notification.CourseId,
				Data = BuildNotificationData(notification, notificationsData),
			};
		}

		private NotificationData BuildNotificationData(Notification notification, NotificationDataStorage notificationsData)
		{
			var data = new NotificationData();
			if (notification is AbstractCommentNotification commentNotification)
				data.Comment = BuildNotificationCommentInfo(notificationsData.CommentsByIds.GetOrDefault(commentNotification.CommentId));
			return data;
		}
	}
}