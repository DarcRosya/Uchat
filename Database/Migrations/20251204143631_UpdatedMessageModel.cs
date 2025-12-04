using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedMessageModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_ChatRooms_ParentChatRoomId",
                table: "ChatRooms");

            migrationBuilder.DropTable(
                name: "ChatRoomMemberPermissions");

            migrationBuilder.DropIndex(
                name: "IX_ChatRooms_ParentChatRoomId",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanCustomizeGroup",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanInviteUsers",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanPinMessages",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanSendFiles",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanSendMessages",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanSendMusic",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanSendPhotos",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanSendStickers",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "DefaultCanSendVideos",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "ParentChatRoomId",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "SlowModeSeconds",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "IsMuted",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "MutedUntil",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ChatRoomMembers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanCustomizeGroup",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanInviteUsers",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanPinMessages",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanSendFiles",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanSendMessages",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanSendMusic",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanSendPhotos",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanSendStickers",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DefaultCanSendVideos",
                table: "ChatRooms",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentChatRoomId",
                table: "ChatRooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlowModeSeconds",
                table: "ChatRooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMuted",
                table: "ChatRoomMembers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MutedUntil",
                table: "ChatRoomMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "ChatRoomMembers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChatRoomMemberPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChatRoomMemberId = table.Column<int>(type: "integer", nullable: false),
                    CanBanUsers = table.Column<bool>(type: "boolean", nullable: true),
                    CanCustomizeGroup = table.Column<bool>(type: "boolean", nullable: true),
                    CanDeleteMessages = table.Column<bool>(type: "boolean", nullable: true),
                    CanInviteUsers = table.Column<bool>(type: "boolean", nullable: true),
                    CanManageInviteLinks = table.Column<bool>(type: "boolean", nullable: true),
                    CanManageTopics = table.Column<bool>(type: "boolean", nullable: true),
                    CanPinMessages = table.Column<bool>(type: "boolean", nullable: true),
                    CanPromoteMembers = table.Column<bool>(type: "boolean", nullable: true),
                    CanRemoveUsers = table.Column<bool>(type: "boolean", nullable: true),
                    CanRestrictUsers = table.Column<bool>(type: "boolean", nullable: true),
                    CanSendFiles = table.Column<bool>(type: "boolean", nullable: true),
                    CanSendMessages = table.Column<bool>(type: "boolean", nullable: true),
                    CanSendMusic = table.Column<bool>(type: "boolean", nullable: true),
                    CanSendPhotos = table.Column<bool>(type: "boolean", nullable: true),
                    CanSendStickers = table.Column<bool>(type: "boolean", nullable: true),
                    CanSendVideos = table.Column<bool>(type: "boolean", nullable: true),
                    CustomTitle = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
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
                name: "IX_ChatRooms_ParentChatRoomId",
                table: "ChatRooms",
                column: "ParentChatRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMemberPermissions_MemberId",
                table: "ChatRoomMemberPermissions",
                column: "ChatRoomMemberId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_ChatRooms_ParentChatRoomId",
                table: "ChatRooms",
                column: "ParentChatRoomId",
                principalTable: "ChatRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
