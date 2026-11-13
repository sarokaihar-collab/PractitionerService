using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PractitionerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrigramSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_practitioners_first_name_active",
                table: "practitioners");

            migrationBuilder.DropIndex(
                name: "ix_practitioners_last_name_active",
                table: "practitioners");

            migrationBuilder.DropIndex(
                name: "ix_practitioners_place_of_work_active",
                table: "practitioners");

            migrationBuilder.DropIndex(
                name: "ix_practitioners_speciality_active",
                table: "practitioners");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "practitioners",
                newName: "phone");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "practitioners",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "ix_practitioners_first_name_trgm",
                table: "practitioners",
                column: "first_name",
                filter: "is_deleted = false")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_practitioners_last_name_trgm",
                table: "practitioners",
                column: "last_name",
                filter: "is_deleted = false")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_practitioners_place_of_work_trgm",
                table: "practitioners",
                column: "place_of_work",
                filter: "is_deleted = false")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_practitioners_speciality_trgm",
                table: "practitioners",
                column: "speciality",
                filter: "is_deleted = false")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_practitioners_first_name_trgm",
                table: "practitioners");

            migrationBuilder.DropIndex(
                name: "ix_practitioners_last_name_trgm",
                table: "practitioners");

            migrationBuilder.DropIndex(
                name: "ix_practitioners_place_of_work_trgm",
                table: "practitioners");

            migrationBuilder.DropIndex(
                name: "ix_practitioners_speciality_trgm",
                table: "practitioners");

            migrationBuilder.RenameColumn(
                name: "phone",
                table: "practitioners",
                newName: "Phone");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "practitioners",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

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
        }
    }
}
