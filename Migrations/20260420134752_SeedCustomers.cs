using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AspNetCore10.OpenTelemetry.Study.Migrations
{
    /// <inheritdoc />
    public partial class SeedCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Customers",
                columns: new[] { "Id", "Email", "Name", "PhoneNumber" },
                values: new object[,]
                {
                    { 1, "alice.johnson@example.com", "Alice Johnson", "555-0101" },
                    { 2, "bob.smith@example.com", "Bob Smith", "555-0102" },
                    { 3, "charlie.brown@example.com", "Charlie Brown", "555-0103" },
                    { 4, "diana.prince@example.com", "Diana Prince", "555-0104" },
                    { 5, "ethan.hunt@example.com", "Ethan Hunt", "555-0105" },
                    { 6, "fiona.gallagher@example.com", "Fiona Gallagher", "555-0106" },
                    { 7, "george.costanza@example.com", "George Costanza", "555-0107" },
                    { 8, "hannah.abbott@example.com", "Hannah Abbott", "555-0108" },
                    { 9, "ian.malcolm@example.com", "Ian Malcolm", "555-0109" },
                    { 10, "jane.doe@example.com", "Jane Doe", "555-0110" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
