using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PractitionerService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniquePhoneIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "phone",
                table: "practitioners",
                newName: "Phone");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "practitioners",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "ux_practitioners_phone_active",
                table: "practitioners",
                column: "Phone",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_practitioners_phone_active",
                table: "practitioners");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "practitioners",
                newName: "phone");

            migrationBuilder.AlterColumn<string>(
                name: "phone",
                table: "practitioners",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
