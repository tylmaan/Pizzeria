using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pizzeria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "saved_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_addresses", x => x.id);
                    table.ForeignKey(
                        name: "fk_saved_addresses_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_saved_addresses_users_customer_id",
                        column: x => x.customer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id_placed_at",
                table: "orders",
                columns: new[] { "customer_id", "placed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_saved_addresses_company_id",
                table: "saved_addresses",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_addresses_customer_id",
                table: "saved_addresses",
                column: "customer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_orders_users_customer_id",
                table: "orders",
                column: "customer_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_orders_users_customer_id",
                table: "orders");

            migrationBuilder.DropTable(
                name: "saved_addresses");

            migrationBuilder.DropIndex(
                name: "ix_orders_customer_id_placed_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "orders");
        }
    }
}
