using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderProvinceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "province_id",
                table: "Orders",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty to comply with non-destructive migration policy.
            // This project must not perform schema rollback steps that can remove existing data.
        }
    }
}
