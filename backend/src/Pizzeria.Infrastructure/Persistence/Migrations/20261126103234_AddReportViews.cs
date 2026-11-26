using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pizzeria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One row per order with its local business day and the moments it reached each stage.
            // Reports are SQL over this view (not computed in memory). Not tenant-filtered by EF:
            // every query on it must filter by company_id explicitly.
            migrationBuilder.Sql("""
                CREATE VIEW report_orders AS
                SELECT
                    o.id,
                    o.company_id,
                    o.location_id,
                    o.number,
                    o.source,
                    o.fulfillment,
                    o.status,
                    o.payment_method,
                    o.items_total,
                    o.delivery_fee,
                    o.total,
                    o.driver_id,
                    o.placed_at,
                    (o.placed_at AT TIME ZONE l.time_zone_id)::date AS business_day,
                    (SELECT min(c.changed_at) FROM order_status_changes c WHERE c.order_id = o.id AND c.to_status = 'Accepted') AS accepted_at,
                    (SELECT min(c.changed_at) FROM order_status_changes c WHERE c.order_id = o.id AND c.to_status = 'Ready') AS ready_at,
                    (SELECT min(c.changed_at) FROM order_status_changes c WHERE c.order_id = o.id AND c.to_status = 'InDelivery') AS out_for_delivery_at,
                    (SELECT min(c.changed_at) FROM order_status_changes c WHERE c.order_id = o.id AND c.to_status IN ('Delivered', 'PickedUp')) AS completed_at
                FROM orders o
                JOIN locations l ON l.id = o.location_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS report_orders;");
        }
    }
}
