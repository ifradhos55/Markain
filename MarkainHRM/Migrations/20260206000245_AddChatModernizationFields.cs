using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OzarkLMS.Migrations
{
    /// <inheritdoc />
    public partial class AddChatModernizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupPhotoUrl",
                table: "ChatGroups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ViewMode",
                table: "ChatGroupMembers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupPhotoUrl",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "ViewMode",
                table: "ChatGroupMembers");
        }
    }
}
