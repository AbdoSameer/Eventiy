using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedEventsAndDeadLetters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "HoldExpiresAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OutboxDeadLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    FailedReason = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    MovedToDeadLetterAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDeadLetters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedEvents",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeadLetters_Domain",
                table: "OutboxDeadLetters",
                column: "Domain");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedEvents_IdempotencyKey",
                table: "ProcessedEvents",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxDeadLetters");

            migrationBuilder.DropTable(
                name: "ProcessedEvents");

            migrationBuilder.DropColumn(
                name: "HoldExpiresAt",
                table: "Bookings");
        }
    }
}
