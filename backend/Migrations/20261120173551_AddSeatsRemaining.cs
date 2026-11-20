using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Admissions.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatsRemaining : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeatsRemaining",
                table: "Classes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeatsRemaining",
                table: "Classes");
        }
    }
}
