using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PractitionerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "practitioners",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    speciality = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    place_of_work = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source_event_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_practitioners", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_practitioners_first_name_active",
                table: "practitioners",
                column: "first_name",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_practitioners_last_name_active",
                table: "practitioners",
                column: "last_name",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_practitioners_place_of_work_active",
                table: "practitioners",
                column: "place_of_work",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_practitioners_speciality_active",
                table: "practitioners",
                column: "speciality",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_practitioners_email_active",
                table: "practitioners",
                column: "email",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ux_practitioners_source_event_id",
                table: "practitioners",
                column: "source_event_id",
                unique: true,
                filter: "source_event_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "practitioners");
        }
    }
}
