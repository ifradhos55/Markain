using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OzarkLMS.Migrations
{
    /// <inheritdoc />
    public partial class AddPostVoteCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DownvoteCount",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "UpvoteCount",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownvoteCount",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "UpvoteCount",
                table: "Posts");
        }
    }
}
