using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogGraphQlApp.Migrations
{
    /// <inheritdoc />
    public partial class GroupChatUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupId",
                table: "GroupMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupMessageId",
                table: "Reactions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "Notifications",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "Notifications",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "RelatedEntityType",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "GroupVideoCalls",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "CameraEnabled",
                table: "GroupVideoCallParticipants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HandRaised",
                table: "GroupVideoCallParticipants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsMuted",
                table: "GroupVideoCallParticipants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ScreenSharing",
                table: "GroupVideoCallParticipants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "GroupMessages",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("UPDATE GroupMessages SET Content = Text WHERE Text IS NOT NULL");

            migrationBuilder.DropColumn(
                name: "Text",
                table: "GroupMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "EditedBy",
                table: "GroupMessages",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "FileUrl",
                table: "GroupMessages",
                type: "varchar(2048)",
                maxLength: 2048,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "GroupMessages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MessageType",
                table: "GroupMessages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "GroupMessages",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "GroupMessages",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedBy",
                table: "GroupMessages",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "GroupMessages",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "RowVersion",
                table: "GroupMessages",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "GroupMessages",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChatGroups",
                type: "datetime(6)",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP(6)",
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<bool>(
                name: "Archived",
                table: "ChatGroups",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "ChatGroups",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "InviteCode",
                table: "ChatGroups",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "ChatGroups",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "ChatGroups",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMessageId",
                table: "ChatGroups",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "MaxMembers",
                table: "ChatGroups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RowVersion",
                table: "ChatGroups",
                type: "timestamp(6)",
                rowVersion: true,
                nullable: false)
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReadAt",
                table: "ChatGroupMembers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Muted",
                table: "ChatGroupMembers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MutedUntil",
                table: "ChatGroupMembers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NotificationLevel",
                table: "ChatGroupMembers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "GroupJoinRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    GroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResolvedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupJoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupJoinRequests_ChatGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ChatGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupJoinRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GroupMessageMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MentionText = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMessageMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMessageMentions_GroupMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "GroupMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMessageMentions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GroupMessageReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMessageReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMessageReads_GroupMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "GroupMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMessageReads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_GroupMessageId",
                table: "Reactions",
                column: "GroupMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_GroupMessageId_UserId",
                table: "Reactions",
                columns: new[] { "GroupMessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupId_CreatedAt",
                table: "GroupMessages",
                columns: new[] { "GroupId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupId_IsPinned",
                table: "GroupMessages",
                columns: new[] { "GroupId", "IsPinned" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupId_MessageType",
                table: "GroupMessages",
                columns: new[] { "GroupId", "MessageType" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupId_SenderId",
                table: "GroupMessages",
                columns: new[] { "GroupId", "SenderId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_ReplyToMessageId",
                table: "GroupMessages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroups_InviteCode",
                table: "ChatGroups",
                column: "InviteCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroups_LastMessageId",
                table: "ChatGroups",
                column: "LastMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupJoinRequests_GroupId_Status",
                table: "GroupJoinRequests",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupJoinRequests_GroupId_UserId",
                table: "GroupJoinRequests",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupJoinRequests_UserId",
                table: "GroupJoinRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessageMentions_MessageId_UserId",
                table: "GroupMessageMentions",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessageMentions_UserId",
                table: "GroupMessageMentions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessageReads_MessageId_UserId",
                table: "GroupMessageReads",
                columns: new[] { "MessageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessageReads_UserId_ReadAt",
                table: "GroupMessageReads",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatGroups_GroupMessages_LastMessageId",
                table: "ChatGroups",
                column: "LastMessageId",
                principalTable: "GroupMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMessages_GroupMessages_ReplyToMessageId",
                table: "GroupMessages",
                column: "ReplyToMessageId",
                principalTable: "GroupMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reactions_GroupMessages_GroupMessageId",
                table: "Reactions",
                column: "GroupMessageId",
                principalTable: "GroupMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatGroups_GroupMessages_LastMessageId",
                table: "ChatGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupMessages_GroupMessages_ReplyToMessageId",
                table: "GroupMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Reactions_GroupMessages_GroupMessageId",
                table: "Reactions");

            migrationBuilder.DropTable(
                name: "GroupJoinRequests");

            migrationBuilder.DropTable(
                name: "GroupMessageMentions");

            migrationBuilder.DropTable(
                name: "GroupMessageReads");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_GroupMessageId",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_GroupMessageId_UserId",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupId_CreatedAt",
                table: "GroupMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupId_IsPinned",
                table: "GroupMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupId_MessageType",
                table: "GroupMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_GroupId_SenderId",
                table: "GroupMessages");

            migrationBuilder.DropIndex(
                name: "IX_GroupMessages_ReplyToMessageId",
                table: "GroupMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatGroups_InviteCode",
                table: "ChatGroups");

            migrationBuilder.DropIndex(
                name: "IX_ChatGroups_LastMessageId",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "GroupMessageId",
                table: "Reactions");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "GroupVideoCalls");

            migrationBuilder.DropColumn(
                name: "CameraEnabled",
                table: "GroupVideoCallParticipants");

            migrationBuilder.DropColumn(
                name: "HandRaised",
                table: "GroupVideoCallParticipants");

            migrationBuilder.DropColumn(
                name: "IsMuted",
                table: "GroupVideoCallParticipants");

            migrationBuilder.DropColumn(
                name: "ScreenSharing",
                table: "GroupVideoCallParticipants");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "EditedBy",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "FileUrl",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "MessageType",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "PinnedBy",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "GroupMessages");

            migrationBuilder.DropColumn(
                name: "Archived",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "InviteCode",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "LastMessageId",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "MaxMembers",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "LastReadAt",
                table: "ChatGroupMembers");

            migrationBuilder.DropColumn(
                name: "Muted",
                table: "ChatGroupMembers");

            migrationBuilder.DropColumn(
                name: "MutedUntil",
                table: "ChatGroupMembers");

            migrationBuilder.DropColumn(
                name: "NotificationLevel",
                table: "ChatGroupMembers");

            migrationBuilder.AddColumn<string>(
                name: "Text",
                table: "GroupMessages",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "ChatGroups",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldDefaultValueSql: "CURRENT_TIMESTAMP(6)");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMessages_GroupId",
                table: "GroupMessages",
                column: "GroupId");
        }
    }
}
