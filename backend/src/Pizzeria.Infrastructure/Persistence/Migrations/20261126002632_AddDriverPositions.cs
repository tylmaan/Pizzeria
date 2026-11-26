using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Pizzeria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "driver_positions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    driver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    point = table.Column<Point>(type: "geometry(Point, 4326)", nullable: false),
                    accuracy_meters = table.Column<double>(type: "double precision", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_driver_positions", x => x.id);
                    table.ForeignKey(
                        name: "fk_driver_positions_driver_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "driver_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_driver_positions_driver_id_recorded_at",
                table: "driver_positions",
                columns: new[] { "driver_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_driver_positions_location_id_recorded_at",
                table: "driver_positions",
                columns: new[] { "location_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_driver_positions_recorded_at",
                table: "driver_positions",
                column: "recorded_at");

            migrationBuilder.CreateIndex(
                name: "ix_driver_positions_shift_id",
                table: "driver_positions",
                column: "shift_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "driver_positions");
        }
    }
}
