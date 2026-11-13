using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Cortex.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_usage_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    answers_used = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage_periods", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_usage_periods_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_storage_usage",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_count = table.Column<int>(type: "integer", nullable: false),
                    storage_bytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_storage_usage", x => x.organization_id);
                    table.ForeignKey(
                        name: "FK_organization_storage_usage_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    price_minor = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ai_answers_per_period = table.Column<int>(type: "integer", nullable: false),
                    max_documents = table.Column<int>(type: "integer", nullable: false),
                    max_storage_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    billing_customer_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    billing_subscription_ref = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_through_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancel_at_period_end = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_subscriptions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_subscriptions_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "plans",
                columns: new[] { "id", "ai_answers_per_period", "code", "currency", "is_active", "max_documents", "max_storage_bytes", "name", "price_minor", "sort_order" },
                values: new object[,]
                {
                    { new Guid("7b1f6f0e-5c0a-4b7e-9d53-0d7c5a1e0001"), 0, "free", "thb", true, 0, 0L, "Free", 0L, 0 },
                    { new Guid("7b1f6f0e-5c0a-4b7e-9d53-0d7c5a1e0002"), 500, "starter", "thb", true, 100, 2147483648L, "Starter", 99000L, 1 },
                    { new Guid("7b1f6f0e-5c0a-4b7e-9d53-0d7c5a1e0003"), 2500, "growth", "thb", true, 500, 10737418240L, "Growth", 249000L, 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_periods_organization_id_period_start",
                table: "ai_usage_periods",
                columns: new[] { "organization_id", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscriptions_billing_subscription_ref",
                table: "organization_subscriptions",
                column: "billing_subscription_ref");

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscriptions_organization_id",
                table: "organization_subscriptions",
                column: "organization_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_subscriptions_plan_id",
                table: "organization_subscriptions",
                column: "plan_id");

            migrationBuilder.CreateIndex(
                name: "IX_plans_code",
                table: "plans",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage_periods");

            migrationBuilder.DropTable(
                name: "organization_storage_usage");

            migrationBuilder.DropTable(
                name: "organization_subscriptions");

            migrationBuilder.DropTable(
                name: "plans");
        }
    }
}
