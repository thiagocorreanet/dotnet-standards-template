using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Module.Talks.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Talks");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "Talks",
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
                name: "Talks",
                schema: "Talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: true),
                    TalkTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TalkDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TalkStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TalkEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_Talks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Certificates",
                schema: "Talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateCode = table.Column<string>(type: "character(12)", fixedLength: true, maxLength: 12, nullable: false),
                    CertificateIssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CertificateDurationMinutes = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certificates_Talks_TalkId",
                        column: x => x.TalkId,
                        principalSchema: "Talks",
                        principalTable: "Talks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TalkContents",
                schema: "Talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContentUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ContentDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_TalkContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalkContents_Talks_TalkId",
                        column: x => x.TalkId,
                        principalSchema: "Talks",
                        principalTable: "Talks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TalkSpeakers",
                schema: "Talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeakerRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_TalkSpeakers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TalkSpeakers_Talks_TalkId",
                        column: x => x.TalkId,
                        principalSchema: "Talks",
                        principalTable: "Talks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Attendances",
                schema: "Talks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TalkId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttendanceRecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Talks_TalkId",
                        column: x => x.TalkId,
                        principalSchema: "Talks",
                        principalTable: "Talks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_CertificateCode",
                schema: "Talks",
                table: "Certificates",
                column: "CertificateCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_DeletedAt",
                schema: "Talks",
                table: "Certificates",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_TalkId_PersonId",
                schema: "Talks",
                table: "Certificates",
                columns: new[] { "TalkId", "PersonId" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "Talks",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "LockedUntil", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_TalkContents_DeletedAt",
                schema: "Talks",
                table: "TalkContents",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TalkContents_TalkId",
                schema: "Talks",
                table: "TalkContents",
                column: "TalkId");

            migrationBuilder.CreateIndex(
                name: "IX_TalkSpeakers_DeletedAt",
                schema: "Talks",
                table: "TalkSpeakers",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TalkSpeakers_TalkId_PersonId",
                schema: "Talks",
                table: "TalkSpeakers",
                columns: new[] { "TalkId", "PersonId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TalkSpeakers_PersonId",
                schema: "Talks",
                table: "TalkSpeakers",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Talks_EventId",
                schema: "Talks",
                table: "Talks",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Talks_DeletedAt",
                schema: "Talks",
                table: "Talks",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Talks_RoomId_TalkStart",
                schema: "Talks",
                table: "Talks",
                columns: new[] { "RoomId", "TalkStart" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_DeletedAt",
                schema: "Talks",
                table: "Attendances",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_TalkId_PersonId",
                schema: "Talks",
                table: "Attendances",
                columns: new[] { "TalkId", "PersonId" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_PersonId",
                schema: "Talks",
                table: "Attendances",
                column: "PersonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Certificates",
                schema: "Talks");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "Talks");

            migrationBuilder.DropTable(
                name: "TalkContents",
                schema: "Talks");

            migrationBuilder.DropTable(
                name: "TalkSpeakers",
                schema: "Talks");

            migrationBuilder.DropTable(
                name: "Attendances",
                schema: "Talks");

            migrationBuilder.DropTable(
                name: "Talks",
                schema: "Talks");
        }
    }
}
