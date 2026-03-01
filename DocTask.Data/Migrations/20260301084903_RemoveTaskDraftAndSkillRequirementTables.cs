using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTask.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTaskDraftAndSkillRequirementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_draft_skill");

            migrationBuilder.DropTable(
                name: "task_skill_requirement");

            migrationBuilder.DropTable(
                name: "task_draft");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_draft",
                columns: table => new
                {
                    draftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    createdBy = table.Column<int>(type: "int", nullable: false),
                    createdTaskId = table.Column<int>(type: "int", nullable: true),
                    fileId = table.Column<int>(type: "int", nullable: false),
                    parentDraftId = table.Column<int>(type: "int", nullable: true),
                    reviewedBy = table.Column<int>(type: "int", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    endDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    estimatedHours = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    rawAIResponse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reviewNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    startDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_draft", x => x.draftId);
                    table.ForeignKey(
                        name: "FK_TaskDraft_Creator",
                        column: x => x.createdBy,
                        principalTable: "user",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDraft_File",
                        column: x => x.fileId,
                        principalTable: "uploadfile",
                        principalColumn: "fileId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskDraft_Parent",
                        column: x => x.parentDraftId,
                        principalTable: "task_draft",
                        principalColumn: "draftId");
                    table.ForeignKey(
                        name: "FK_TaskDraft_Reviewer",
                        column: x => x.reviewedBy,
                        principalTable: "user",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskDraft_Task",
                        column: x => x.createdTaskId,
                        principalTable: "task",
                        principalColumn: "taskId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_skill_requirement",
                columns: table => new
                {
                    taskSkillRequirementId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    skillId = table.Column<int>(type: "int", nullable: false),
                    taskId = table.Column<int>(type: "int", nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    importance = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    isAutoExtracted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    requiredLevel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_skill_requirement", x => x.taskSkillRequirementId);
                    table.ForeignKey(
                        name: "FK_TaskSkillRequirement_Skill",
                        column: x => x.skillId,
                        principalTable: "skill",
                        principalColumn: "skillId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskSkillRequirement_Task",
                        column: x => x.taskId,
                        principalTable: "task",
                        principalColumn: "taskId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_draft_skill",
                columns: table => new
                {
                    draftSkillId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    draftId = table.Column<int>(type: "int", nullable: false),
                    skillId = table.Column<int>(type: "int", nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())"),
                    importance = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    isAIExtracted = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    requiredLevel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_draft_skill", x => x.draftSkillId);
                    table.ForeignKey(
                        name: "FK_TaskDraftSkill_Draft",
                        column: x => x.draftId,
                        principalTable: "task_draft",
                        principalColumn: "draftId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskDraftSkill_Skill",
                        column: x => x.skillId,
                        principalTable: "skill",
                        principalColumn: "skillId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_draft_createdBy",
                table: "task_draft",
                column: "createdBy");

            migrationBuilder.CreateIndex(
                name: "IX_task_draft_createdTaskId",
                table: "task_draft",
                column: "createdTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_task_draft_fileId",
                table: "task_draft",
                column: "fileId");

            migrationBuilder.CreateIndex(
                name: "IX_task_draft_parentDraftId",
                table: "task_draft",
                column: "parentDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_task_draft_reviewedBy",
                table: "task_draft",
                column: "reviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_task_draft_skill_skillId",
                table: "task_draft_skill",
                column: "skillId");

            migrationBuilder.CreateIndex(
                name: "UQ_TaskDraftSkill_Draft_Skill",
                table: "task_draft_skill",
                columns: new[] { "draftId", "skillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_skill_requirement_skillId",
                table: "task_skill_requirement",
                column: "skillId");

            migrationBuilder.CreateIndex(
                name: "IX_task_skill_requirement_taskId",
                table: "task_skill_requirement",
                column: "taskId");
        }
    }
}
