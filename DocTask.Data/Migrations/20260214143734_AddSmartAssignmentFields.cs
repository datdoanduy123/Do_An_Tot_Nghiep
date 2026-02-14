using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTask.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSmartAssignmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CurrentWorkloadHours",
                table: "employee_profile",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WeeklyCapacity",
                table: "employee_profile",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_task_parentTaskId",
                table: "task",
                column: "parentTaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_Task_ParentTask",
                table: "task",
                column: "parentTaskId",
                principalTable: "task",
                principalColumn: "taskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Task_ParentTask",
                table: "task");

            migrationBuilder.DropIndex(
                name: "IX_task_parentTaskId",
                table: "task");

            migrationBuilder.DropColumn(
                name: "CurrentWorkloadHours",
                table: "employee_profile");

            migrationBuilder.DropColumn(
                name: "WeeklyCapacity",
                table: "employee_profile");
        }
    }
}
