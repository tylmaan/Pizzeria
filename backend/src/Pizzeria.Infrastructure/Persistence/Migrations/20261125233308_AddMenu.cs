using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pizzeria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "half_and_half_pricing",
                table: "companies",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "MoreExpensiveHalf");

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "category_sizes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category_sizes", x => x.id);
                    table.ForeignKey(
                        name: "fk_category_sizes_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modifier_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    min_select = table.Column<int>(type: "integer", nullable: false),
                    max_select = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modifier_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_modifier_groups_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_modifier_groups_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    allows_halves = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.ForeignKey(
                        name: "fk_products_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_products_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "modifiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modifiers", x => x.id);
                    table.ForeignKey(
                        name: "fk_modifiers_modifier_groups_modifier_group_id",
                        column: x => x.modifier_group_id,
                        principalTable: "modifier_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_modifier_groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    modifier_group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_modifier_groups", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_modifier_groups_modifier_groups_modifier_group_id",
                        column: x => x.modifier_group_id,
                        principalTable: "modifier_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_modifier_groups_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    size_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_prices", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_prices_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modifier_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    size_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    modifier_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_modifier_prices", x => x.id);
                    table.ForeignKey(
                        name: "fk_modifier_prices_modifiers_modifier_id",
                        column: x => x.modifier_id,
                        principalTable: "modifiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_company_id",
                table: "categories",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_category_sizes_category_id",
                table: "category_sizes",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_modifier_groups_category_id",
                table: "modifier_groups",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_modifier_groups_company_id",
                table: "modifier_groups",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_modifier_prices_modifier_id_size_id",
                table: "modifier_prices",
                columns: new[] { "modifier_id", "size_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_modifiers_modifier_group_id",
                table: "modifiers",
                column: "modifier_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_modifier_groups_modifier_group_id",
                table: "product_modifier_groups",
                column: "modifier_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_modifier_groups_product_id_modifier_group_id",
                table: "product_modifier_groups",
                columns: new[] { "product_id", "modifier_group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_prices_product_id_size_id",
                table: "product_prices",
                columns: new[] { "product_id", "size_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_company_id",
                table: "products",
                column: "company_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "category_sizes");

            migrationBuilder.DropTable(
                name: "modifier_prices");

            migrationBuilder.DropTable(
                name: "product_modifier_groups");

            migrationBuilder.DropTable(
                name: "product_prices");

            migrationBuilder.DropTable(
                name: "modifiers");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "modifier_groups");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropColumn(
                name: "half_and_half_pricing",
                table: "companies");
        }
    }
}
