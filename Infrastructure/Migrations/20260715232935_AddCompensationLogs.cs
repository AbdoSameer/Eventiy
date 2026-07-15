using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompensationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompensationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NextRetryOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessingLock = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessingLockedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompensationLogs_BookingId",
                table: "CompensationLogs",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CompensationLogs_IdempotencyKey",
                table: "CompensationLogs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompensationLogs_Processing",
                table: "CompensationLogs",
                columns: new[] { "ProcessingLock", "ProcessingLockedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CompensationLogs_ReadyForProcessing",
                table: "CompensationLogs",
                columns: new[] { "ProcessedOnUtc", "NextRetryOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompensationLogs");
        }
    }
}
