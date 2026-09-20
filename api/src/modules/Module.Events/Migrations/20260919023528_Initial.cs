using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Module.Events.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Events");

            migrationBuilder.CreateTable(
                name: "Events",
                schema: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EventStartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventEndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VenueId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventRemoteUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EventStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EventMaximumCapacity = table.Column<int>(type: "integer", nullable: true),
                    EventCancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", maxLength: 200, nullable: false),
                    OccurredOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TraceParent = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Registrations",
                schema: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegistrationStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RegistrationRegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RegistrationCanceledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Registrations_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "Events",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventStartDate",
                schema: "Events",
                table: "Events",
                column: "EventStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventStatus",
                schema: "Events",
                table: "Events",
                column: "EventStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Events_DeletedAt",
                schema: "Events",
                table: "Events",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueId",
                schema: "Events",
                table: "Events",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_EventId_RegistrationStatus",
                schema: "Events",
                table: "Registrations",
                columns: new[] { "EventId", "RegistrationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_EventId_PersonId_Confirmed",
                schema: "Events",
                table: "Registrations",
                columns: new[] { "EventId", "PersonId" },
                unique: true,
                filter: "\"RegistrationStatus\" = 'Confirmed'");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_DeletedAt",
                schema: "Events",
                table: "Registrations",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "Events",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "LockedUntil", "OccurredOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Registrations",
                schema: "Events");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "Events");

            migrationBuilder.DropTable(
                name: "Events",
                schema: "Events");
        }
    }
}
