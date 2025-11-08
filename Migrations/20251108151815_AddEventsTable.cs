using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Konfucjusz.Migrations
{
    /// <inheritdoc />
    public partial class AddEventsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    creation_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    event_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    enlisting_start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    enlisting_end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    allowed_anonymous_enlisting = table.Column<bool>(type: "boolean", nullable: false),
                    allow_anonymous_streaming = table.Column<bool>(type: "boolean", nullable: false),
                    searchable = table.Column<bool>(type: "boolean", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "events");
        }
    }
}
