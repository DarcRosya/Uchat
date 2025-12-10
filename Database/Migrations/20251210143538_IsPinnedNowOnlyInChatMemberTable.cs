using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Database.Migrations
{
    /// <inheritdoc />
    public partial class IsPinnedNowOnlyInChatMemberTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "NotificationsEnabled",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "IconUrl",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "MaxMembers",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "TotalMessagesCount",
                table: "ChatRooms");

            migrationBuilder.AlterColumn<string>(
                name: "Nickname",
                table: "Contacts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Nickname",
                table: "Contacts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "Contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "Contacts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsEnabled",
                table: "Contacts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "Contacts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ChatRooms",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconUrl",
                table: "ChatRooms",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMembers",
                table: "ChatRooms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TotalMessagesCount",
                table: "ChatRooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
