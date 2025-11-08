using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Konfucjusz.Migrations
{
    /// <inheritdoc />
    public partial class AddEventParticipantsAndEnlistmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "consent_text",
                table: "events",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "enable_waitlist",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "max_participants",
                table: "events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "require_organizer_approval",
                table: "events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "event_participants",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    surname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    consent_given = table.Column<bool>(type: "boolean", nullable: false),
                    consent_given_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consent_text_snapshot = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_event_participants_events_event_id",
                        column: x => x.event_id,
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_event_participants_user_account_user_id",
                        column: x => x.user_id,
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_events_slug",
                table: "events",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_event_id_created_at",
                table: "event_participants",
                columns: new[] { "event_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_event_id_status",
                table: "event_participants",
                columns: new[] { "event_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_normalized_email",
                table: "event_participants",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "IX_event_participants_user_id",
                table: "event_participants",
                column: "user_id");

            // Add partial unique indexes for active participants (Postgres-specific)
            // Logged-in users: unique (event_id, user_id) where user_id IS NOT NULL and status is active
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX idx_event_participants_user_unique 
                ON event_participants(event_id, user_id)
                WHERE user_id IS NOT NULL 
                  AND status NOT IN ('Cancelled', 'Removed', 'Declined');
            ");

            // Anonymous users: unique (event_id, normalized_email) where user_id IS NULL and status is active
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX idx_event_participants_anonymous_email_unique 
                ON event_participants(event_id, normalized_email)
                WHERE user_id IS NULL 
                  AND status NOT IN ('Cancelled', 'Removed', 'Declined');
            ");

            // Add trigger function and trigger for auto-updating updated_at
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION update_event_participant_updated_at()
                RETURNS TRIGGER AS $$
                BEGIN
                    NEW.updated_at = now();
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trigger_event_participant_updated_at
                    BEFORE UPDATE ON event_participants
                    FOR EACH ROW
                    EXECUTE FUNCTION update_event_participant_updated_at();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop trigger and function first
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trigger_event_participant_updated_at ON event_participants;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS update_event_participant_updated_at();");

            // Drop partial unique indexes
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_event_participants_user_unique;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_event_participants_anonymous_email_unique;");

            migrationBuilder.DropTable(
                name: "event_participants");

            migrationBuilder.DropIndex(
                name: "IX_events_slug",
                table: "events");

            migrationBuilder.DropColumn(
                name: "consent_text",
                table: "events");

            migrationBuilder.DropColumn(
                name: "enable_waitlist",
                table: "events");

            migrationBuilder.DropColumn(
                name: "max_participants",
                table: "events");

            migrationBuilder.DropColumn(
                name: "require_organizer_approval",
                table: "events");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "events");
        }
    }
}
