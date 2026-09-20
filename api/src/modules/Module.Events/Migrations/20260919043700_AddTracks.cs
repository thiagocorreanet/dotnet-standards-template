using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Module.Events.Migrations
{
    /// <inheritdoc />
    public partial class AddTracks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tracks",
                schema: "Events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TrackDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TrackColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
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
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tracks_Events_EventId",
                        column: x => x.EventId,
                        principalSchema: "Events",
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_EventId_TrackName",
                schema: "Events",
                table: "Tracks",
                columns: new[] { "EventId", "TrackName" },
                unique: true,
                filter: "\"DeletedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_DeletedAt",
                schema: "Events",
                table: "Tracks",
                column: "DeletedAt");

            // Compatibilidade com eventos já existentes. A expressão mantém versão/variante de UUID v7.
            migrationBuilder.Sql("""
                INSERT INTO "Events"."Tracks"
                    ("Id", "EventId", "TrackName", "TrackDescription", "TrackColor", "CreatedAt", "CreatedBy", "IsActive")
                SELECT
                    (lpad(to_hex((extract(epoch from clock_timestamp()) * 1000)::bigint), 12, '0') ||
                     '7' || substr(md5(random()::text), 2, 3) || '8' || substr(md5(random()::text), 1, 3) ||
                     substr(md5(random()::text), 1, 12))::uuid,
                    e."Id", 'Trilha única', NULL, '#2563EB', now(), 'migration', TRUE
                FROM "Events"."Events" e
                WHERE e."DeletedAt" IS NULL
                  AND NOT EXISTS (SELECT 1 FROM "Events"."Tracks" t WHERE t."EventId" = e."Id" AND t."DeletedAt" IS NULL);

                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tracks",
                schema: "Events");
        }
    }
}
