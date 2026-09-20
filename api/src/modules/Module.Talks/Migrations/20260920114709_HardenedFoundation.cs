using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Module.Talks.Migrations
{
    /// <inheritdoc />
    public partial class HardenedFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "Talks",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_TalkId_PersonId",
                schema: "Talks",
                table: "Certificates");

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimToken",
                schema: "Talks",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeadLetteredAt",
                schema: "Talks",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                schema: "Talks",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EventNameSnapshot",
                schema: "Talks",
                table: "Certificates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TalkStartSnapshot",
                schema: "Talks",
                table: "Certificates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "TalkTitleSnapshot",
                schema: "Talks",
                table: "Certificates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PersonNameSnapshot",
                schema: "Talks",
                table: "Certificates",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CommandReceipts",
                schema: "Talks",
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
                schema: "Talks",
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
                name: "IX_OutboxMessages_ClaimToken",
                schema: "Talks",
                table: "OutboxMessages",
                column: "ClaimToken");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "Talks",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "DeadLetteredAt", "NextAttemptAt", "LockedUntil", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_TalkId_PersonId",
                schema: "Talks",
                table: "Certificates",
                columns: new[] { "TalkId", "PersonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommandReceipts_CommittedAt",
                schema: "Talks",
                table: "CommandReceipts",
                column: "CommittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandReceipts",
                schema: "Talks");

            migrationBuilder.DropTable(
                name: "OutboxReplayAudit",
                schema: "Talks");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ClaimToken",
                schema: "Talks",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "Talks",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_TalkId_PersonId",
                schema: "Talks",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "ClaimToken",
                schema: "Talks",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "DeadLetteredAt",
                schema: "Talks",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                schema: "Talks",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "EventNameSnapshot",
                schema: "Talks",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "TalkStartSnapshot",
                schema: "Talks",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "TalkTitleSnapshot",
                schema: "Talks",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "PersonNameSnapshot",
                schema: "Talks",
                table: "Certificates");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "Talks",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "LockedUntil", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_TalkId_PersonId",
                schema: "Talks",
                table: "Certificates",
                columns: new[] { "TalkId", "PersonId" });
        }
    }
}
