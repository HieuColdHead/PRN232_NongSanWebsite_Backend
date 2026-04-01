using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSuggestedVariantToMealComboItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "suggested_unit_price",
                table: "MealComboItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "suggested_variant_id",
                table: "MealComboItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MealComboItems_suggested_variant_id",
                table: "MealComboItems",
                column: "suggested_variant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_MealComboItems_ProductVariants_suggested_variant_id",
                table: "MealComboItems",
                column: "suggested_variant_id",
                principalTable: "ProductVariants",
                principalColumn: "variant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MealComboItems_ProductVariants_suggested_variant_id",
                table: "MealComboItems");

            migrationBuilder.DropIndex(
                name: "IX_MealComboItems_suggested_variant_id",
                table: "MealComboItems");

            migrationBuilder.DropColumn(
                name: "suggested_unit_price",
                table: "MealComboItems");

            migrationBuilder.DropColumn(
                name: "suggested_variant_id",
                table: "MealComboItems");
        }
    }
}
