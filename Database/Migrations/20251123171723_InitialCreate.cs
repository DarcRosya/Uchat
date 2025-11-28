using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Uchat.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Salt = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Bio = table.Column<string>(type: "TEXT", maxLength: 190, nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    AvatarUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "NOW()"),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    LanguageCode = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false, defaultValue: "en"),
                    IsBlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    IconUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ParentChatRoomId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatorId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "NOW()"),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxMembers = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultCanSendMessages = table.Column<bool>(type: "INTEGER", nullable: true),
                    DefaultCanSendPhotos = table.Column<bool>(type: "INTEGER", nullable: true),
                    DefaultCanSendVideos = table.Column<bool>(type: "INTEGER", nullable: true),
                    DefaultCanSendStickers = table.Column<bool>(type: "INTEGER", nullable: true),
                    DefaultCanSendMusic = table.Column<bool>(type: "INTEGER", nullable: true),
                    DefaultCanSendFiles = table.Column<bool>(type: "INTEGER", nullable: true),
                    DefaultCanInviteUsers = table.Column<bool>(type: "INTEGER", nullable: true),
                    DefaultCanPinMessages = table.Column<bool>(type: "INTEGER", nullable: true),
                    DefaultCanCustomizeGroup = table.Column<bool>(type: "INTEGER", nullable: true),
                    SlowModeSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalMessagesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRooms_ChatRooms_ParentChatRoomId",
                        column: x => x.ParentChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRooms_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<int>(type: "INTEGER", nullable: false),
                    ContactUserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PrivateNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "NOW()"),
                    IsBlocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    NotificationsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastMessageAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contacts_Users_ContactUserId",
                        column: x => x.ContactUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Contacts_Users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Friendships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SenderId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiverId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "NOW()"),
                    AcceptedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friendships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Friendships_Users_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Friendships_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatRoomMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChatRoomId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "NOW()"),
                    InvitedById = table.Column<int>(type: "INTEGER", nullable: true),
                    IsMuted = table.Column<bool>(type: "INTEGER", nullable: false),
                    MutedUntil = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRoomMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_ChatRooms_ChatRoomId",
                        column: x => x.ChatRoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_Users_InvitedById",
                        column: x => x.InvitedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatRoomMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatRoomMemberPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChatRoomMemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    CanSendMessages = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSendPhotos = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSendVideos = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSendStickers = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSendMusic = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanSendFiles = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanDeleteMessages = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanPinMessages = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanInviteUsers = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanRemoveUsers = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanBanUsers = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanRestrictUsers = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanPromoteMembers = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanCustomizeGroup = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanManageTopics = table.Column<bool>(type: "INTEGER", nullable: true),
                    CanManageInviteLinks = table.Column<bool>(type: "INTEGER", nullable: true),
                    CustomTitle = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatRoomMemberPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatRoomMemberPermissions_ChatRoomMembers_ChatRoomMemberId",
                        column: x => x.ChatRoomMemberId,
                        principalTable: "ChatRoomMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMemberPermissions_MemberId",
                table: "ChatRoomMemberPermissions",
                column: "ChatRoomMemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_ChatRoom_User",
                table: "ChatRoomMembers",
                columns: new[] { "ChatRoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_InvitedById",
                table: "ChatRoomMembers",
                column: "InvitedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_UserId",
                table: "ChatRoomMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_CreatorId",
                table: "ChatRooms",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_LastActivityAt",
                table: "ChatRooms",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_ParentChatRoomId",
                table: "ChatRooms",
                column: "ParentChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_Type",
                table: "ChatRooms",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_ContactUserId",
                table: "Contacts",
                column: "ContactUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Owner_Contact",
                table: "Contacts",
                columns: new[] { "OwnerId", "ContactUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_Receiver_Status",
                table: "Friendships",
                columns: new[] { "ReceiverId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_ReceiverId",
                table: "Friendships",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_Sender_Receiver",
                table: "Friendships",
                columns: new[] { "SenderId", "ReceiverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_Sender_Status",
                table: "Friendships",
                columns: new[] { "SenderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_Status",
                table: "Friendships",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatRoomMemberPermissions");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "Friendships");

            migrationBuilder.DropTable(
                name: "ChatRoomMembers");

            migrationBuilder.DropTable(
                name: "ChatRooms");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
