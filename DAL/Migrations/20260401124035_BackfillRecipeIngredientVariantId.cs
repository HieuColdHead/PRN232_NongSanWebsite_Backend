using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class BackfillRecipeIngredientVariantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill variant_id for existing recipe ingredients:
            // If a recipe ingredient has a product_id and variant_id is null,
            // and that product has exactly 1 non-deleted variant, then set variant_id to that variant.
            migrationBuilder.Sql(@"
UPDATE ""RecipeIngredients"" ri
SET ""variant_id"" = pv.""variant_id""
FROM ""ProductVariants"" pv
JOIN (
    SELECT ""product_id""
    FROM ""ProductVariants""
    WHERE ""is_deleted"" = FALSE
    GROUP BY ""product_id""
    HAVING COUNT(*) = 1
) single_variant_products ON single_variant_products.""product_id"" = pv.""product_id""
WHERE ri.""variant_id"" IS NULL
  AND ri.""product_id"" IS NOT NULL
  AND pv.""is_deleted"" = FALSE
  AND pv.""product_id"" = ri.""product_id"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
