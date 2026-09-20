using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Module.Talks.Shared;
namespace Module.Talks.Migrations;
[DbContext(typeof(TalksDbContext))]
[Migration("20260921000000_RoomExclusion")]
public sealed class RoomExclusion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        CREATE EXTENSION IF NOT EXISTS btree_gist;
        ALTER TABLE "Talks"."Talks"
            ADD CONSTRAINT "CK_Talks_Period" CHECK ("TalkEnd" > "TalkStart");
        ALTER TABLE "Talks"."Talks"
            ADD CONSTRAINT "EX_Talks_RoomPeriod" EXCLUDE USING gist
            ("RoomId" WITH =, tstzrange("TalkStart", "TalkEnd", '[)') WITH &&)
            WHERE ("RoomId" IS NOT NULL AND "DeletedAt" IS NULL AND "IsActive");
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Talks"."Talks" DROP CONSTRAINT "EX_Talks_RoomPeriod";
        ALTER TABLE "Talks"."Talks" DROP CONSTRAINT "CK_Talks_Period";
        """);
}
