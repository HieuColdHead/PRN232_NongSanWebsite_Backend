using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddGhnShipmentIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "cod_amount",
                table: "Payments",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_status",
                table: "Orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "district_code",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "province_code",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recipient_name",
                table: "Orders",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "recipient_phone",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ward_code",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ghn_order_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    delivery_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    raw_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tracking_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    shipping_fee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    cod_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.shipment_id);
                    table.ForeignKey(
                        name: "FK_Shipments_Orders_order_id",
                        column: x => x.order_id,
                        principalTable: "Orders",
                        principalColumn: "order_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentStatusUpdates",
                columns: table => new
                {
                    status_update_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    new_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    raw_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentStatusUpdates", x => x.status_update_id);
                    table.ForeignKey(
                        name: "FK_ShipmentStatusUpdates_Shipments_shipment_id",
                        column: x => x.shipment_id,
                        principalTable: "Shipments",
                        principalColumn: "shipment_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_GhnOrderCode",
                table: "Shipments",
                column: "ghn_order_code");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId",
                table: "Shipments",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentStatusUpdates_ShipmentId",
                table: "ShipmentStatusUpdates",
                column: "shipment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty to comply with non-destructive migration policy.
            // This project must not perform schema rollback steps that can remove existing data.
        }
    }
}
