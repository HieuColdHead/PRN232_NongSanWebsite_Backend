using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMealComboToCartAndOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_ProductVariants_variant_id",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_ProductVariants_variant_id",
                table: "OrderDetails");

            migrationBuilder.AlterColumn<Guid>(
                name: "variant_id",
                table: "OrderDetails",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "meal_combo_id",
                table: "OrderDetails",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "variant_id",
                table: "CartItems",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "meal_combo_id",
                table: "CartItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderDetails_meal_combo_id",
                table: "OrderDetails",
                column: "meal_combo_id");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_meal_combo_id",
                table: "CartItems",
                column: "meal_combo_id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_MealCombos_meal_combo_id",
                table: "CartItems",
                column: "meal_combo_id",
                principalTable: "MealCombos",
                principalColumn: "meal_combo_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_ProductVariants_variant_id",
                table: "CartItems",
                column: "variant_id",
                principalTable: "ProductVariants",
                principalColumn: "variant_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_MealCombos_meal_combo_id",
                table: "OrderDetails",
                column: "meal_combo_id",
                principalTable: "MealCombos",
                principalColumn: "meal_combo_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_ProductVariants_variant_id",
                table: "OrderDetails",
                column: "variant_id",
                principalTable: "ProductVariants",
                principalColumn: "variant_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_MealCombos_meal_combo_id",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_ProductVariants_variant_id",
                table: "CartItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_MealCombos_meal_combo_id",
                table: "OrderDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetails_ProductVariants_variant_id",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_OrderDetails_meal_combo_id",
                table: "OrderDetails");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_meal_combo_id",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "meal_combo_id",
                table: "OrderDetails");

            migrationBuilder.DropColumn(
                name: "meal_combo_id",
                table: "CartItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "variant_id",
                table: "OrderDetails",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "variant_id",
                table: "CartItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_ProductVariants_variant_id",
                table: "CartItems",
                column: "variant_id",
                principalTable: "ProductVariants",
                principalColumn: "variant_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetails_ProductVariants_variant_id",
                table: "OrderDetails",
                column: "variant_id",
                principalTable: "ProductVariants",
                principalColumn: "variant_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
