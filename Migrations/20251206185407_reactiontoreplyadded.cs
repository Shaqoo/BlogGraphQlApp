using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogGraphQlApp.Migrations
{
    /// <inheritdoc />
    public partial class reactiontoreplyadded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NestedReplyCount",
                table: "Replies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReactionCount",
                table: "Replies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReplyId",
                table: "Reactions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_ReplyId",
                table: "Reactions",
                column: "ReplyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reactions_Replies_ReplyId",
                table: "Reactions",
                column: "ReplyId",
                principalTable: "Replies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reactions_Replies_ReplyId",
                table: "Reactions");

            migrationBuilder.DropIndex(
                name: "IX_Reactions_ReplyId",
                table: "Reactions");

            migrationBuilder.DropColumn(
                name: "NestedReplyCount",
                table: "Replies");

            migrationBuilder.DropColumn(
                name: "ReactionCount",
                table: "Replies");

            migrationBuilder.DropColumn(
                name: "ReplyId",
                table: "Reactions");
        }
    }
}
