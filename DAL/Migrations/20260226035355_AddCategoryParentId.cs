using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryParentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parent_id",
                table: "Categories",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_parent_id",
                table: "Categories",
                column: "parent_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_parent_id",
                table: "Categories",
                column: "parent_id",
                principalTable: "Categories",
                principalColumn: "category_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_parent_id",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_parent_id",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "Categories");
        }
    }
}
