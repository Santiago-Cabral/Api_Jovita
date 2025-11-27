using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ForrajeriaJovitaAPI.Migrations
{
    public partial class AddDecimalFix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- SALES ---
            migrationBuilder.AlterColumn<decimal>(
                name: "Subtotal",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountTotal",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<decimal>(
                name: "Total",
                table: "Sales",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));

            // --- SALE ITEMS ---
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "SalesItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<decimal>(
                name: "UnitPrice",
                table: "SalesItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<decimal>(
                name: "Discount",
                table: "SalesItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<decimal>(
                name: "ConversionToBase",
                table: "SalesItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<decimal>(
                name: "DeductedBaseQuantity",
                table: "SalesItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));

            // --- PRODUCT STOCK ---
            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "ProductsStocks",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse changes

            migrationBuilder.AlterColumn<int>(
                name: "Subtotal",
                table: "Sales",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "DiscountTotal",
                table: "Sales",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "Total",
                table: "Sales",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            // SALEITEM
            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "SalesItems",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "UnitPrice",
                table: "SalesItems",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "Discount",
                table: "SalesItems",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "ConversionToBase",
                table: "SalesItems",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<int>(
                name: "DeductedBaseQuantity",
                table: "SalesItems",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            // STOCK
            migrationBuilder.AlterColumn<int>(
                name: "Quantity",
                table: "ProductsStocks",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");
        }
    }
}
