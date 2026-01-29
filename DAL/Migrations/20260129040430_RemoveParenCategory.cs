using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveParenCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_parent_category_id",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_parent_category_id",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "parent_category_id",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parent_category_id",
                table: "Categories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_parent_category_id",
                table: "Categories",
                column: "parent_category_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_parent_category_id",
                table: "Categories",
                column: "parent_category_id",
                principalTable: "Categories",
                principalColumn: "category_id");
        }
    }
}
