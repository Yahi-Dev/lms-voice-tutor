using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LmsTutor.Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTutorExchanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutorExchanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LessonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    InputMode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Answer = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CitedSectionKeys = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PromptTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    CompletionTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    LatencyMilliseconds = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutorExchanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TutorExchanges_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TutorExchanges_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutorExchanges_LessonId",
                table: "TutorExchanges",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "IX_TutorExchanges_UserId_ClientMessageId",
                table: "TutorExchanges",
                columns: new[] { "UserId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutorExchanges_UserId_LessonId_CreatedAt",
                table: "TutorExchanges",
                columns: new[] { "UserId", "LessonId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutorExchanges");
        }
    }
}
