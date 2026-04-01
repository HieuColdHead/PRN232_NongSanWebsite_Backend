using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantToRecipeIngredients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "variant_id",
                table: "RecipeIngredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_variant_id",
                table: "RecipeIngredients",
                column: "variant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredients_ProductVariants_variant_id",
                table: "RecipeIngredients",
                column: "variant_id",
                principalTable: "ProductVariants",
                principalColumn: "variant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeIngredients_ProductVariants_variant_id",
                table: "RecipeIngredients");

            migrationBuilder.DropIndex(
                name: "IX_RecipeIngredients_variant_id",
                table: "RecipeIngredients");

            migrationBuilder.DropColumn(
                name: "variant_id",
                table: "RecipeIngredients");
        }
    }
}
