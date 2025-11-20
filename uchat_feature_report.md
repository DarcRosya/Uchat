# Uchat.Database Model & Feature Extension Report

## Summary of Changes

### 1. User Entity
- Added fields: `Bio`, `PhoneNumber`, `BirthDate`, `Language`, `TimeZone`.
- Added `UserRole` enum and `Role` property.

### 2. Message Entity
- Added support for:
  - Message reactions (new `MessageReaction` entity, navigation property `Reactions`).
  - Forwarded messages (`ForwardedFromMessageId`, `ForwardedFromUserId`, navigation properties).
  - Mentions (`MentionedUserIds` as JSON).
  - Delivery status (`DeliveryStatus` enum and property).

### 3. ChatRoom Entity
- Added channel support (`Channel` in `ChatRoomType` enum).
- Added group settings: `AllowMembersToInvite`, `AllowMembersToSendMessages`, `AllowMembersToSendMedia`, `SlowModeSeconds`.
- Added group statistics: `TotalMessagesCount`, `LastActivityAt`.
- Added category support: new `ChatRoomCategory` entity, `CategoryId` and navigation property.

### 4. ChatRoomMember Entity
- Added custom roles: new `CustomChatRoomRole` entity, `CustomRoleId` and navigation property.
- Added member statistics: `MessageCount`, `LastMessageAt`.
- Added notification settings: `NotificationsEnabled`, `NotificationSound`.
- Added admin notes: `Notes`.

### 5. Contact Entity
- Added contact groups: new `ContactGroup` entity, `GroupId` and navigation property.
- Added notes: `Notes`.
- Added interaction history: `LastMessageAt`, `MessageCount`.
- Added custom settings: `CustomRingtone`, `NotificationsEnabled`, `ShowTypingIndicator`.
- Added two-way friendship: new `Friendship` entity and `FriendshipStatus` enum.

### 6. Database Context (UchatDbContext)
- Added seed data for an admin user.
- Added a global query filter for soft delete on messages.

---

