using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Module.Talks.Migrations
{
    /// <inheritdoc />
    public partial class LinkTrack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TrackId",
                schema: "Talks",
                table: "Talks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE "Talks"."Talks" p
                SET "TrackId" = (
                    SELECT tr."Id" FROM "Events"."Tracks" tr
                    WHERE tr."EventId" = p."EventId" AND tr."DeletedAt" IS NULL
                    ORDER BY tr."CreatedAt" LIMIT 1)
                WHERE p."TrackId" = '00000000-0000-0000-0000-000000000000'::uuid;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Talks_TrackId",
                schema: "Talks",
                table: "Talks",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Talks_TrackId",
                schema: "Talks",
                table: "Talks");

            migrationBuilder.DropColumn(
                name: "TrackId",
                schema: "Talks",
                table: "Talks");
        }
    }
}
