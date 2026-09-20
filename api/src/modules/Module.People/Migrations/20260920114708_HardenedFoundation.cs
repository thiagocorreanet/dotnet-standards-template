using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Module.People.Migrations
{
    /// <inheritdoc />
    public partial class HardenedFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "People",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "People",
                table: "People",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimToken",
                schema: "People",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadLetteredAt",
                schema: "People",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                schema: "People",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommandReceipts",
                schema: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxReplayAudit",
                schema: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxReplayAudit", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_People_UserId",
                schema: "People",
                table: "People",
                column: "UserId",
                unique: true,
                filter: "\"DeletedAt\" IS NULL AND \"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ClaimToken",
                schema: "People",
                table: "OutboxMessages",
                column: "ClaimToken");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "People",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "DeadLetteredAt", "NextAttemptAt", "LockedUntil", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandReceipts_CommittedAt",
                schema: "People",
                table: "CommandReceipts",
                column: "CommittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandReceipts",
                schema: "People");

            migrationBuilder.DropTable(
                name: "OutboxReplayAudit",
                schema: "People");

            migrationBuilder.DropIndex(
                name: "IX_People_UserId",
                schema: "People",
                table: "People");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ClaimToken",
                schema: "People",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "People",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "People",
                table: "People");

            migrationBuilder.DropColumn(
                name: "ClaimToken",
                schema: "People",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                schema: "People",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "People",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "People",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "LockedUntil", "OccurredOn" });
        }
    }
}
