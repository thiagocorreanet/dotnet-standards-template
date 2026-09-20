using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Module.People.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "People");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "People",
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
                name: "People",
                schema: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PersonEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PersonPhone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PersonDocument = table.Column<string>(type: "character(11)", fixedLength: true, maxLength: 11, nullable: true),
                    PersonCompany = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    PersonJobTitle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PersonShortBio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PersonPhotoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_People", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Pending",
                schema: "People",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOn", "LockedUntil", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "IX_People_DeletedAt",
                schema: "People",
                table: "People",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_People_PersonDocument",
                schema: "People",
                table: "People",
                column: "PersonDocument",
                unique: true,
                filter: "\"DeletedAt\" IS NULL AND \"PersonDocument\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_People_PersonEmail",
                schema: "People",
                table: "People",
                column: "PersonEmail",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_People_PersonName",
                schema: "People",
                table: "People",
                column: "PersonName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "People");

            migrationBuilder.DropTable(
                name: "People",
                schema: "People");
        }
    }
}
