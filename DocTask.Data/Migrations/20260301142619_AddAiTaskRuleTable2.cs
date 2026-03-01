using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTask.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiTaskRuleTable2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TaskType",
                table: "task",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "task");
        }
    }
}
