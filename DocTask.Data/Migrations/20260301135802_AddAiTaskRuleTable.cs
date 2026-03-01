using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DocTask.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiTaskRuleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_task_rule",
                columns: table => new
                {
                    ruleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ruleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    keyword = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    skillName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    requiredLevel = table.Column<int>(type: "int", nullable: true),
                    importance = table.Column<int>(type: "int", nullable: true),
                    phaseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    phaseRatio = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    phaseHours = table.Column<int>(type: "int", nullable: true),
                    sortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    isActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    updatedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_task_rule", x => x.ruleId);
                });

            migrationBuilder.InsertData(
                table: "ai_task_rule",
                columns: new[] { "ruleId", "createdAt", "importance", "isActive", "keyword", "phaseHours", "phaseName", "phaseRatio", "requiredLevel", "ruleType", "skillName", "sortOrder", "updatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, 40, "Phân tích & Thiết kế", 0.2m, null, "DefaultPhase", null, 1, null },
                    { 2, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, 80, "Phát triển Backend", 0.3m, null, "DefaultPhase", null, 2, null },
                    { 3, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, 80, "Phát triển Frontend", 0.3m, null, "DefaultPhase", null, 3, null },
                    { 4, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, 30, "Kiểm thử (Testing)", 0.1m, null, "DefaultPhase", null, 4, null },
                    { 5, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, true, null, 20, "Triển khai & Bàn giao", 0.1m, null, "DefaultPhase", null, 5, null },
                    { 6, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "ASP.NET", null, null, null, 4, "SkillKeyword", "ASP.NET Core", 1, null },
                    { 7, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, ".NET CORE", null, null, null, 4, "SkillKeyword", "ASP.NET Core", 2, null },
                    { 8, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "C#", null, null, null, 4, "SkillKeyword", "C# .NET", 3, null },
                    { 9, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "NODEJS", null, null, null, 4, "SkillKeyword", "NodeJS", 4, null },
                    { 10, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "NODE.JS", null, null, null, 4, "SkillKeyword", "NodeJS", 5, null },
                    { 11, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "JAVA", null, null, null, 4, "SkillKeyword", "Java/Spring", 6, null },
                    { 12, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "SPRING", null, null, null, 4, "SkillKeyword", "Java/Spring", 7, null },
                    { 13, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "REACT", null, null, null, 4, "SkillKeyword", "React", 8, null },
                    { 14, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "ANGULAR", null, null, null, 4, "SkillKeyword", "Angular", 9, null },
                    { 15, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "VUE", null, null, null, 4, "SkillKeyword", "Vue", 10, null },
                    { 16, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "SQL SERVER", null, null, null, 3, "SkillKeyword", "SQL Server", 11, null },
                    { 17, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "MSSQL", null, null, null, 3, "SkillKeyword", "SQL Server", 12, null },
                    { 18, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "MYSQL", null, null, null, 3, "SkillKeyword", "MySQL", 13, null },
                    { 19, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "POSTGRES", null, null, null, 3, "SkillKeyword", "PostgreSQL", 14, null },
                    { 20, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "MONGODB", null, null, null, 3, "SkillKeyword", "MongoDB", 15, null },
                    { 21, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "JWT", null, null, null, 3, "SkillKeyword", "Security", 16, null },
                    { 22, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "OAUTH2", null, null, null, 3, "SkillKeyword", "Security", 17, null },
                    { 23, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "RBAC", null, null, null, 3, "SkillKeyword", "Security", 18, null },
                    { 24, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "DOCKER", null, null, null, 3, "SkillKeyword", "DevOps", 19, null },
                    { 25, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "CI/CD", null, null, null, 3, "SkillKeyword", "DevOps", 20, null },
                    { 26, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "DEVOPS", null, null, null, 3, "SkillKeyword", "DevOps", 21, null },
                    { 27, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "AZURE", null, null, null, 3, "SkillKeyword", "DevOps", 22, null },
                    { 28, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "UNIT TEST", null, null, null, 3, "SkillKeyword", "Software Testing", 23, null },
                    { 29, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "TESTING", null, null, null, 3, "SkillKeyword", "Software Testing", 24, null },
                    { 30, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "VNPAY", null, null, null, 3, "SkillKeyword", "Payment Integration", 25, null },
                    { 31, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "MOMO", null, null, null, 3, "SkillKeyword", "Payment Integration", 26, null },
                    { 32, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "STRIPE", null, null, null, 3, "SkillKeyword", "Payment Integration", 27, null },
                    { 33, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "ZALOPAY", null, null, null, 3, "SkillKeyword", "Payment Integration", 28, null },
                    { 34, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "API", null, null, null, 4, "ModuleKeywordSkill", "ASP.NET Core", 1, null },
                    { 35, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "API", null, null, null, 4, "ModuleKeywordSkill", "C# .NET", 2, null },
                    { 36, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, true, "XỬ LÝ", null, null, null, 4, "ModuleKeywordSkill", "ASP.NET Core", 3, null },
                    { 37, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "GIAO DIỆN", null, null, null, 4, "ModuleKeywordSkill", "React", 4, null },
                    { 38, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "FRONTEND", null, null, null, 4, "ModuleKeywordSkill", "React", 5, null },
                    { 39, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "UI", null, null, null, 4, "ModuleKeywordSkill", "React", 6, null },
                    { 40, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "CSDL", null, null, null, 3, "ModuleKeywordSkill", "SQL Server", 7, null },
                    { 41, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "DATABASE", null, null, null, 3, "ModuleKeywordSkill", "SQL Server", 8, null },
                    { 42, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "THANH TOÁN", null, null, null, 3, "ModuleKeywordSkill", "Payment Integration", 9, null },
                    { 43, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "PAYMENT", null, null, null, 3, "ModuleKeywordSkill", "Payment Integration", 10, null },
                    { 44, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "BẢO MẬT", null, null, null, 3, "ModuleKeywordSkill", "Security", 11, null },
                    { 45, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "CI/CD", null, null, null, 3, "ModuleKeywordSkill", "DevOps", 12, null },
                    { 46, new DateTime(2026, 3, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 2, true, "DOCKER", null, null, null, 3, "ModuleKeywordSkill", "DevOps", 13, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiTaskRule_Type_Active",
                table: "ai_task_rule",
                columns: new[] { "ruleType", "isActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_task_rule");
        }
    }
}
