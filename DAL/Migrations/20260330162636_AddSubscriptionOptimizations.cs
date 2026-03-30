using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealCombos",
                columns: table => new
                {
                    meal_combo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    target_people_count = table.Column<int>(type: "integer", nullable: false),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    diet_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    base_price = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealCombos", x => x.meal_combo_id);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    instructions = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    cooking_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    servings = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.recipe_id);
                    table.ForeignKey(
                        name: "FK_Recipes_Users_author_id",
                        column: x => x.author_id,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_delivery_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    shipping_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recipient_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    recipient_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_processing = table.Column<bool>(type: "boolean", nullable: false),
                    pricing_policy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_processed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.subscription_id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealComboItems",
                columns: table => new
                {
                    meal_combo_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_combo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealComboItems", x => x.meal_combo_item_id);
                    table.ForeignKey(
                        name: "FK_MealComboItems_MealCombos_meal_combo_id",
                        column: x => x.meal_combo_id,
                        principalTable: "MealCombos",
                        principalColumn: "meal_combo_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealComboItems_Products_product_id",
                        column: x => x.product_id,
                        principalTable: "Products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeIngredients",
                columns: table => new
                {
                    recipe_ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ingredient_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredients", x => x.recipe_ingredient_id);
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Products_product_id",
                        column: x => x.product_id,
                        principalTable: "Products",
                        principalColumn: "product_id");
                    table.ForeignKey(
                        name: "FK_RecipeIngredients_Recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "Recipes",
                        principalColumn: "recipe_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionItems",
                columns: table => new
                {
                    subscription_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    fixed_price = table.Column<decimal>(type: "numeric", nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionItems", x => x.subscription_item_id);
                    table.ForeignKey(
                        name: "FK_SubscriptionItems_Products_product_id",
                        column: x => x.product_id,
                        principalTable: "Products",
                        principalColumn: "product_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SubscriptionItems_Subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "Subscriptions",
                        principalColumn: "subscription_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealComboItems_meal_combo_id",
                table: "MealComboItems",
                column: "meal_combo_id");

            migrationBuilder.CreateIndex(
                name: "IX_MealComboItems_product_id",
                table: "MealComboItems",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_product_id",
                table: "RecipeIngredients",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredients_recipe_id",
                table: "RecipeIngredients",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_author_id",
                table: "Recipes",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionItems_product_id",
                table: "SubscriptionItems",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionItems_subscription_id",
                table: "SubscriptionItems",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_user_id",
                table: "Subscriptions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealComboItems");

            migrationBuilder.DropTable(
                name: "RecipeIngredients");

            migrationBuilder.DropTable(
                name: "SubscriptionItems");

            migrationBuilder.DropTable(
                name: "MealCombos");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "Subscriptions");
        }
    }
}
