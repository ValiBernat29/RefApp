using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RefApp.Migrations
{
    /// <inheritdoc />
    public partial class AddHasCarColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasCar",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PreferredRole",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasCar",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PreferredRole",
                table: "AspNetUsers");
        }
    }
}
