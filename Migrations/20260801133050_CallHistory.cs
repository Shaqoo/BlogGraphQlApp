using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogGraphQlApp.Migrations
{
    /// <inheritdoc />
    public partial class CallHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CallHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CallId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CallType = table.Column<int>(type: "int", nullable: false),
                    CallerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RecipientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    GroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RoomName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EndedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CallHistories_ChatGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ChatGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallHistories_Users_CallerId",
                        column: x => x.CallerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallHistories_Users_EndedByUserId",
                        column: x => x.EndedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CallHistories_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GroupCallParticipantHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CallHistoryId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    JoinedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LeftAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupCallParticipantHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupCallParticipantHistories_CallHistories_CallHistoryId",
                        column: x => x.CallHistoryId,
                        principalTable: "CallHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupCallParticipantHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CallHistories_CallerId",
                table: "CallHistories",
                column: "CallerId");

            migrationBuilder.CreateIndex(
                name: "IX_CallHistories_CallId",
                table: "CallHistories",
                column: "CallId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CallHistories_EndedByUserId",
                table: "CallHistories",
                column: "EndedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CallHistories_GroupId",
                table: "CallHistories",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CallHistories_RecipientId",
                table: "CallHistories",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_CallHistories_StartedAt",
                table: "CallHistories",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CallHistories_Status",
                table: "CallHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCallParticipantHistories_CallHistoryId",
                table: "GroupCallParticipantHistories",
                column: "CallHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupCallParticipantHistories_UserId",
                table: "GroupCallParticipantHistories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GroupCallParticipantHistories");

            migrationBuilder.DropTable(
                name: "CallHistories");
        }
    }
}
