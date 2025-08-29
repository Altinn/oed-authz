using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace oed_authz.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_table_eventcursor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eventcursor",
                schema: "oedauthz",
                columns: table => new
                {
                    estateSsn = table.Column<string>(type: "character(11)", fixedLength: true, maxLength: 11, nullable: false),
                    eventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    lastTimestampProcessed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("eventcursor_pkey", x => new { x.estateSsn, x.eventType });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eventcursor",
                schema: "oedauthz");
        }
    }
}
