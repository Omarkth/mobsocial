﻿using System;
using System.Linq;
using mobSocial.Core.Infrastructure.AppEngine;
using mobSocial.Data.Constants;
using mobSocial.Data.Entity.EntityProperties;
using mobSocial.Data.Entity.Settings;
using mobSocial.Data.Entity.Users;
using mobSocial.Data.Enum;
using mobSocial.Services.Extensions;
using mobSocial.Services.Helpers;
using mobSocial.Services.MediaServices;
using mobSocial.Services.Notifications;
using mobSocial.Services.Social;
using mobSocial.WebApi.Configuration.Infrastructure;
using mobSocial.WebApi.Models.Users;
using Newtonsoft.Json;

namespace mobSocial.WebApi.Extensions.ModelExtensions
{
    public static class UserExtensions
    {
        public static UserResponseModel ToModel(this User user, IMediaService mediaService, MediaSettings mediaSettings, IFollowService followService = null, IFriendService friendService = null, INotificationService notificationService = null)
        {
            var currentUser = ApplicationContext.Current.CurrentUser;
            var model = new UserResponseModel()
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Name = user.Name,
                DateCreatedUtc = user.DateCreated,
                DateCreatedLocal = DateTimeHelper.GetDateInUserTimeZone(user.DateCreated, DateTimeKind.Utc, user),
                UserName = user.UserName,
                CoverImageUrl = mediaService.GetPictureUrl(user.GetPropertyValueAs<int>(PropertyNames.DefaultCoverId), PictureSizeNames.MediumCover, true),
                ProfileImageUrl = mediaService.GetPictureUrl(user.GetPropertyValueAs<int>(PropertyNames.DefaultPictureId), PictureSizeNames.MediumProfileImage, true),
                Active = user.Active,
                Educations = user.Educations?.Select(x => x.ToModel(mediaService)).ToList(),
                SeName = user.GetPermalink()?.Slug
            };
            //TODO: Put capability check instead of administration check, that'd be more scalable
            if (currentUser.IsAdministrator() && user.LastLoginDate.HasValue)
            {
                model.LastLoginDateUtc = user.LastLoginDate;
                model.LastLoginDateLocal = DateTimeHelper.GetDateInUserTimeZone(user.LastLoginDate.Value,
                    DateTimeKind.Utc, user);
            }
            if (followService != null)
            {
                model.FollowerCount = followService.GetFollowerCount<User>(user.Id);
                model.FollowingCount = followService.Count(x => x.UserId == user.Id);
                model.CanFollow = currentUser != null && currentUser.Id != user.Id; //todo: Check if the current user can be followed or not according to user's personalized setting (to be implementedas well)
                if (model.CanFollow)
                    model.FollowStatus = followService.GetCustomerFollow<User>(currentUser.Id, user.Id) == null ? 0 : 1;
            }

            if (friendService != null)
            {
                model.FriendCount =
                friendService.Count(x => x.Confirmed && (x.FromCustomerId == user.Id || x.ToCustomerId == user.Id));
                model.FriendStatus = currentUser != null ? friendService.GetFriendStatus(currentUser.Id, user.Id) : FriendStatus.None;
            }

            if (!string.IsNullOrEmpty(model.CoverImageUrl) && !string.IsNullOrEmpty(model.ProfileImageUrl))
                return model;

            if (string.IsNullOrEmpty(model.CoverImageUrl))
                model.CoverImageUrl = mediaSettings.DefaultUserProfileCoverUrl;
            if (string.IsNullOrEmpty(model.ProfileImageUrl))
                model.ProfileImageUrl = mediaSettings.DefaultUserProfileImageUrl;

            //we can send notifications as well if it's a user asking for his own details
            if (currentUser != null && currentUser.Id == user.Id && notificationService != null)
            {
                var now = DateTime.UtcNow;
                var qNotifications =
                    notificationService.Get(x => x.UserId == currentUser.Id && x.PublishDateTime <= now,
                        x => new { x.Id }, false, earlyLoad: x => x.NotificationEvent);

                var unreadCount = qNotifications.Count(x => !x.IsRead);
                var notifications = qNotifications.Take(15).ToList();
                model.Notifications = notifications.Select(x => x.ToModel()).ToList();
                model.UnreadNotificationCount = unreadCount;
            }

            //check if user is online or not
            model.IsOnline = true;
            return model;
        }

        public static UserEntityModel ToEntityModel(this User user, IMediaService mediaService, MediaSettings mediaSettings)
        {
            var userCoverId = user.GetPropertyValueAs<int>(PropertyNames.DefaultCoverId);
            var userProfileImageId = user.GetPropertyValueAs<int>(PropertyNames.DefaultPictureId);
            var model = new UserEntityModel()
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Name = user.Name,
                UserName = user.UserName,
                Email = user.Email,
                Active = user.Active,
                Remarks = user.Remarks,
                RoleIds = user.UserRoles.Select(x => x.RoleId).ToList(),
                CoverImageId = userCoverId,
                ProfileImageId = userProfileImageId
            };
            //TODO: Put capability check instead of administration check, that'd be more scalable
            if (ApplicationContext.Current.CurrentUser.IsAdministrator() && user.LastLoginDate.HasValue)
            {
                model.LastLoginDateUtc = user.LastLoginDate;
                model.LastLoginDateLocal = DateTimeHelper.GetDateInUserTimeZone(user.LastLoginDate.Value,
                    DateTimeKind.Utc, user);
            }
            model.CoverImageUrl = userCoverId == 0 ? mediaSettings.DefaultUserProfileCoverUrl : mediaService.GetPictureUrl(userCoverId, PictureSizeNames.MediumCover) ?? mediaSettings.DefaultUserProfileCoverUrl;
            model.ProfileImageUrl = userProfileImageId == 0 ? mediaSettings.DefaultUserProfileImageUrl : mediaService.GetPictureUrl(userProfileImageId, PictureSizeNames.MediumProfileImage) ?? mediaSettings.DefaultUserProfileImageUrl;
            return model;
        }

        public static UserEntityPublicModel ToEntityPublicModel(this User user, IMediaService mediaService, MediaSettings mediaSettings)
        {
            var userCoverId = user.GetPropertyValueAs<int>(PropertyNames.DefaultCoverId);
            var userProfileImageId = user.GetPropertyValueAs<int>(PropertyNames.DefaultPictureId);
            var model = new UserEntityPublicModel()
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Name = user.Name,
                UserName = user.UserName,
                Email = user.Email,
                CoverImageId = userCoverId,
                ProfileImageId = userProfileImageId
            };
            model.CoverImageUrl = userCoverId == 0 ? mediaSettings.DefaultUserProfileCoverUrl : mediaService.GetPictureUrl(userCoverId, PictureSizeNames.MediumCover) ?? mediaSettings.DefaultUserProfileCoverUrl;
            model.ProfileImageUrl = userProfileImageId == 0 ? mediaSettings.DefaultUserProfileImageUrl : mediaService.GetPictureUrl(userProfileImageId, PictureSizeNames.MediumProfileImage) ?? mediaSettings.DefaultUserProfileImageUrl;
            return model;
        }

        public static UserConfigurationModel ToUserConfigurationModel(this EntityProperty entityrProperty)
        {
            return new UserConfigurationModel()
            {
                PropertyName = entityrProperty.PropertyName,
                PropertyValue = JsonConvert.DeserializeAnonymousType(entityrProperty.Value, "")
            };
        }
    }
}