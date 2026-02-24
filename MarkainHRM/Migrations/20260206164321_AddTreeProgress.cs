using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OzarkLMS.Migrations
{
    /// <inheritdoc />
    public partial class AddTreeProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TreeProgress",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TreeProgress",
                table: "Users");
        }
    }
}
