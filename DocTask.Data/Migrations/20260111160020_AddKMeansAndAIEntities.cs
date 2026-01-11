using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocTask.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddKMeansAndAIEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "frequency",
                columns: table => new
                {
                    frequencyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    frequencyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    frequencyDetail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    intervalValue = table.Column<int>(type: "int", nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_frequency", x => x.frequencyId);
                });

            migrationBuilder.CreateTable(
                name: "org",
                columns: table => new
                {
                    orgId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    orgName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    parentOrgId = table.Column<int>(type: "int", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org", x => x.orgId);
                });

            migrationBuilder.CreateTable(
                name: "period",
                columns: table => new
                {
                    periodId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    periodName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    startDate = table.Column<DateOnly>(type: "date", nullable: false),
                    endDate = table.Column<DateOnly>(type: "date", nullable: false),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_period", x => x.periodId);
                });

            migrationBuilder.CreateTable(
                name: "position",
                columns: table => new
                {
                    positionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    positionName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position", x => x.positionId);
                });

            migrationBuilder.CreateTable(
                name: "role",
                columns: table => new
                {
                    roleid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    rolename = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    createdat = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role", x => x.roleid);
                });

            migrationBuilder.CreateTable(
                name: "skill",
                columns: table => new
                {
                    skillId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    skillName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    isActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill", x => x.skillId);
                });

            migrationBuilder.CreateTable(
                name: "frequency_detail",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    frequencyId = table.Column<int>(type: "int", nullable: false),
                    dayOfWeek = table.Column<int>(type: "int", nullable: true),
                    dayOfMonth = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_frequency_detail", x => x.id);
                    table.ForeignKey(
                        name: "fk_frequency_detail_frequency",
                        column: x => x.frequencyId,
                        principalTable: "frequency",
                        principalColumn: "frequencyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unit",
                columns: table => new
                {
                    unitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    orgId = table.Column<int>(type: "int", nullable: false),
                    unitName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "official"),
                    unitParent = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit", x => x.unitId);
                    table.ForeignKey(
                        name: "fkUnitOrg",
                        column: x => x.orgId,
                        principalTable: "org",
                        principalColumn: "orgId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_context",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    contextName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    contextDescription = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    fileId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_context", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "assignment_history",
                columns: table => new
                {
                    assignmentHistoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    taskId = table.Column<int>(type: "int", nullable: false),
                    assignedToUserId = table.Column<int>(type: "int", nullable: false),
                    assignedByUserId = table.Column<int>(type: "int", nullable: false),
                    assignmentMethod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Manual"),
                    matchScore = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    clusterIdUsed = table.Column<int>(type: "int", nullable: true),
                    assignmentReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    assignedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    isCompleted = table.Column<bool>(type: "bit", nullable: true),
                    completedOnTime = table.Column<bool>(type: "bit", nullable: true),
                    completedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_history", x => x.assignmentHistoryId);
                });

            migrationBuilder.CreateTable(
                name: "employee_cluster",
                columns: table => new
                {
                    employeeClusterId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userId = table.Column<int>(type: "int", nullable: false),
                    clusterId = table.Column<int>(type: "int", nullable: false),
                    clusterName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    distanceToCenter = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    confidenceScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    modelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "1.0"),
                    clusteredAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    featureVector = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_cluster", x => x.employeeClusterId);
                });

            migrationBuilder.CreateTable(
                name: "employee_profile",
                columns: table => new
                {
                    employeeProfileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userId = table.Column<int>(type: "int", nullable: false),
                    totalYearsOfExperience = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    averageSkillLevel = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    productivityScore = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    currentWorkloadPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    availableHoursPerWeek = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    averagePerformanceRating = table.Column<decimal>(type: "decimal(3,2)", nullable: true),
                    completedTasksCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    onTimeCompletionCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    lastUpdated = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_profile", x => x.employeeProfileId);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                columns: table => new
                {
                    notificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userId = table.Column<int>(type: "int", nullable: true),
                    taskId = table.Column<int>(type: "int", nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    isRead = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification", x => x.notificationId);
                });

            migrationBuilder.CreateTable(
                name: "performance_review",
                columns: table => new
                {
                    performanceReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userId = table.Column<int>(type: "int", nullable: false),
                    reviewedByUserId = table.Column<int>(type: "int", nullable: false),
                    reviewPeriod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    overallRating = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    technicalSkillsRating = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    teamworkRating = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    timelinessRating = table.Column<decimal>(type: "decimal(3,2)", nullable: false),
                    comments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reviewDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performance_review", x => x.performanceReviewId);
                });

            migrationBuilder.CreateTable(
                name: "progress",
                columns: table => new
                {
                    progressId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    taskId = table.Column<int>(type: "int", nullable: false),
                    periodId = table.Column<int>(type: "int", nullable: true),
                    percentageComplete = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    updatedBy = table.Column<int>(type: "int", nullable: true),
                    updatedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    fileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    filePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    proposal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    feedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PeriodIndex = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_progress", x => x.progressId);
                    table.ForeignKey(
                        name: "fkProgressPeriod",
                        column: x => x.periodId,
                        principalTable: "period",
                        principalColumn: "periodId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reminder",
                columns: table => new
                {
                    reminderid = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    taskid = table.Column<int>(type: "int", nullable: false),
                    periodid = table.Column<int>(type: "int", nullable: true),
                    message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    triggertime = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    isauto = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    createdby = table.Column<int>(type: "int", nullable: true),
                    createdat = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    isnotified = table.Column<bool>(type: "bit", nullable: true, defaultValue: false),
                    notifiedat = table.Column<DateTime>(type: "datetime", nullable: true),
                    notificationid = table.Column<int>(type: "int", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false, defaultValue: ""),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminder", x => x.reminderid);
                    table.ForeignKey(
                        name: "reminder_ibfk_2",
                        column: x => x.periodid,
                        principalTable: "period",
                        principalColumn: "periodId");
                    table.ForeignKey(
                        name: "reminder_ibfk_4",
                        column: x => x.notificationid,
                        principalTable: "notification",
                        principalColumn: "notificationId");
                });

            migrationBuilder.CreateTable(
                name: "reminderunit",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    reminderid = table.Column<int>(type: "int", nullable: false),
                    unitid = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reminderunit", x => x.id);
                    table.ForeignKey(
                        name: "fk_reminder",
                        column: x => x.reminderid,
                        principalTable: "reminder",
                        principalColumn: "reminderid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_unit",
                        column: x => x.unitid,
                        principalTable: "unit",
                        principalColumn: "unitId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_review",
                columns: table => new
                {
                    reviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    progressId = table.Column<int>(type: "int", nullable: false),
                    reviewerId = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__report_r__2ECD6E04C573B4B5", x => x.reviewId);
                    table.ForeignKey(
                        name: "FK_ReportReview_Progress",
                        column: x => x.progressId,
                        principalTable: "progress",
                        principalColumn: "progressId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reportsummary",
                columns: table => new
                {
                    reportId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    taskId = table.Column<int>(type: "int", nullable: true),
                    periodId = table.Column<int>(type: "int", nullable: true),
                    summary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    createdBy = table.Column<int>(type: "int", nullable: true),
                    reportFile = table.Column<int>(type: "int", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reportsummary", x => x.reportId);
                    table.ForeignKey(
                        name: "fkReportPeriod",
                        column: x => x.periodId,
                        principalTable: "period",
                        principalColumn: "periodId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task",
                columns: table => new
                {
                    taskId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    assignerId = table.Column<int>(type: "int", nullable: true),
                    assigneeId = table.Column<int>(type: "int", nullable: true),
                    orgId = table.Column<int>(type: "int", nullable: true),
                    periodId = table.Column<int>(type: "int", nullable: true),
                    attachedFile = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "pending"),
                    priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true, defaultValue: "medium"),
                    startDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    dueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    unitId = table.Column<int>(type: "int", nullable: true),
                    frequencyId = table.Column<int>(type: "int", nullable: true),
                    percentagecomplete = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    parentTaskId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: true),
                    estimatedHours = table.Column<decimal>(type: "decimal(7,2)", nullable: true),
                    isAIGenerated = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    isAutoAssigned = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task", x => x.taskId);
                    table.ForeignKey(
                        name: "fk_taskitem_frequency",
                        column: x => x.frequencyId,
                        principalTable: "frequency",
                        principalColumn: "frequencyId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_skill_requirement",
                columns: table => new
                {
                    taskSkillRequirementId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    taskId = table.Column<int>(type: "int", nullable: false),
                    skillId = table.Column<int>(type: "int", nullable: false),
                    requiredLevel = table.Column<int>(type: "int", nullable: false),
                    importance = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    isAutoExtracted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
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
                name: "taskunitassignment",
                columns: table => new
                {
                    TaskUnitAssignmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taskunitassignment", x => x.TaskUnitAssignmentId);
                    table.ForeignKey(
                        name: "taskunitassignment_ibfk_1",
                        column: x => x.TaskId,
                        principalTable: "task",
                        principalColumn: "taskId");
                    table.ForeignKey(
                        name: "taskunitassignment_ibfk_2",
                        column: x => x.UnitId,
                        principalTable: "unit",
                        principalColumn: "unitId");
                });

            migrationBuilder.CreateTable(
                name: "taskassignees",
                columns: table => new
                {
                    TaskId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taskassignees", x => new { x.TaskId, x.UserId });
                    table.ForeignKey(
                        name: "taskassignees_ibfk_1",
                        column: x => x.TaskId,
                        principalTable: "task",
                        principalColumn: "taskId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "unituser",
                columns: table => new
                {
                    unitUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    unitId = table.Column<int>(type: "int", nullable: false),
                    userId = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    level = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unituser", x => x.unitUserId);
                    table.ForeignKey(
                        name: "fkUnitUserUnit",
                        column: x => x.unitId,
                        principalTable: "unit",
                        principalColumn: "unitId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    userId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    fullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    phoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    orgId = table.Column<int>(type: "int", nullable: true),
                    unitId = table.Column<int>(type: "int", nullable: true),
                    userParent = table.Column<int>(type: "int", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    refreshtoken = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    refreshtokenexpirytime = table.Column<DateTime>(type: "datetime", nullable: true),
                    ResetToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResetTokenExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    role = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false, defaultValue: "0"),
                    unitUserId = table.Column<int>(type: "int", nullable: true),
                    positionId = table.Column<int>(type: "int", nullable: true),
                    positionName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.userId);
                    table.ForeignKey(
                        name: "fk_user_unitUser",
                        column: x => x.unitUserId,
                        principalTable: "unituser",
                        principalColumn: "unitUserId");
                    table.ForeignKey(
                        name: "user_ibfk_1",
                        column: x => x.positionId,
                        principalTable: "position",
                        principalColumn: "positionId");
                    table.ForeignKey(
                        name: "user_ibfk_2",
                        column: x => x.orgId,
                        principalTable: "org",
                        principalColumn: "orgId");
                    table.ForeignKey(
                        name: "user_ibfk_3",
                        column: x => x.unitId,
                        principalTable: "unit",
                        principalColumn: "unitId");
                    table.ForeignKey(
                        name: "user_ibfk_4",
                        column: x => x.userParent,
                        principalTable: "user",
                        principalColumn: "userId");
                });

            migrationBuilder.CreateTable(
                name: "uploadfile",
                columns: table => new
                {
                    fileId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    fileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    filePath = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    uploadedBy = table.Column<int>(type: "int", nullable: true),
                    uploadedAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploadfile", x => x.fileId);
                    table.ForeignKey(
                        name: "fkUploadFileUploadedBy",
                        column: x => x.uploadedBy,
                        principalTable: "user",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_skill",
                columns: table => new
                {
                    userSkillId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userId = table.Column<int>(type: "int", nullable: false),
                    skillId = table.Column<int>(type: "int", nullable: false),
                    proficiencyLevel = table.Column<int>(type: "int", nullable: false),
                    yearsOfExperience = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    verifiedBy = table.Column<int>(type: "int", nullable: true),
                    verifiedAt = table.Column<DateTime>(type: "datetime", nullable: true),
                    createdAt = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    updatedAt = table.Column<DateTime>(type: "datetime", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_skill", x => x.userSkillId);
                    table.ForeignKey(
                        name: "FK_UserSkill_Skill",
                        column: x => x.skillId,
                        principalTable: "skill",
                        principalColumn: "skillId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSkill_User",
                        column: x => x.userId,
                        principalTable: "user",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserSkill_Verifier",
                        column: x => x.verifiedBy,
                        principalTable: "user",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "userrole",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userid = table.Column<int>(type: "int", nullable: false),
                    roleid = table.Column<int>(type: "int", nullable: false),
                    createdat = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userrole", x => x.id);
                    table.ForeignKey(
                        name: "fk_userrole_role",
                        column: x => x.roleid,
                        principalTable: "role",
                        principalColumn: "roleid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_userrole_user",
                        column: x => x.userid,
                        principalTable: "user",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workload_metric",
                columns: table => new
                {
                    workloadMetricId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    userId = table.Column<int>(type: "int", nullable: false),
                    activeTasksCount = table.Column<int>(type: "int", nullable: false),
                    estimatedHoursRemaining = table.Column<decimal>(type: "decimal(7,2)", nullable: false),
                    workloadPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    availableHours = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    snapshotDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workload_metric", x => x.workloadMetricId);
                    table.ForeignKey(
                        name: "FK_WorkloadMetric_User",
                        column: x => x.userId,
                        principalTable: "user",
                        principalColumn: "userId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_context_fileId",
                table: "agent_context",
                column: "fileId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_history_assignedByUserId",
                table: "assignment_history",
                column: "assignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_history_assignedToUserId",
                table: "assignment_history",
                column: "assignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_assignment_history_taskId",
                table: "assignment_history",
                column: "taskId");

            migrationBuilder.CreateIndex(
                name: "IX_employee_cluster_userId",
                table: "employee_cluster",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_profile_userId",
                table: "employee_profile",
                column: "userId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_frequency_detail_frequencyId",
                table: "frequency_detail",
                column: "frequencyId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_taskId",
                table: "notification",
                column: "taskId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_userId",
                table: "notification",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_performance_review_reviewedByUserId",
                table: "performance_review",
                column: "reviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_performance_review_userId",
                table: "performance_review",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_progress_periodId",
                table: "progress",
                column: "periodId");

            migrationBuilder.CreateIndex(
                name: "IX_progress_taskId",
                table: "progress",
                column: "taskId");

            migrationBuilder.CreateIndex(
                name: "IX_progress_updatedBy",
                table: "progress",
                column: "updatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_reminder_createdby",
                table: "reminder",
                column: "createdby");

            migrationBuilder.CreateIndex(
                name: "IX_reminder_notificationid",
                table: "reminder",
                column: "notificationid");

            migrationBuilder.CreateIndex(
                name: "IX_reminder_periodid",
                table: "reminder",
                column: "periodid");

            migrationBuilder.CreateIndex(
                name: "IX_reminder_taskid",
                table: "reminder",
                column: "taskid");

            migrationBuilder.CreateIndex(
                name: "IX_reminder_UserId",
                table: "reminder",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_reminderunit_reminderid",
                table: "reminderunit",
                column: "reminderid");

            migrationBuilder.CreateIndex(
                name: "IX_reminderunit_unitid",
                table: "reminderunit",
                column: "unitid");

            migrationBuilder.CreateIndex(
                name: "IX_report_review_progressId",
                table: "report_review",
                column: "progressId");

            migrationBuilder.CreateIndex(
                name: "IX_report_review_reviewerId",
                table: "report_review",
                column: "reviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_reportsummary_createdBy",
                table: "reportsummary",
                column: "createdBy");

            migrationBuilder.CreateIndex(
                name: "IX_reportsummary_periodId",
                table: "reportsummary",
                column: "periodId");

            migrationBuilder.CreateIndex(
                name: "IX_reportsummary_reportFile",
                table: "reportsummary",
                column: "reportFile");

            migrationBuilder.CreateIndex(
                name: "IX_reportsummary_taskId",
                table: "reportsummary",
                column: "taskId");

            migrationBuilder.CreateIndex(
                name: "IX_task_assigneeId",
                table: "task",
                column: "assigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_task_frequencyId",
                table: "task",
                column: "frequencyId");

            migrationBuilder.CreateIndex(
                name: "IX_task_skill_requirement_skillId",
                table: "task_skill_requirement",
                column: "skillId");

            migrationBuilder.CreateIndex(
                name: "IX_task_skill_requirement_taskId",
                table: "task_skill_requirement",
                column: "taskId");

            migrationBuilder.CreateIndex(
                name: "IX_taskassignees_UserId",
                table: "taskassignees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_taskunitassignment_TaskId",
                table: "taskunitassignment",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_taskunitassignment_UnitId",
                table: "taskunitassignment",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_unit_orgId",
                table: "unit",
                column: "orgId");

            migrationBuilder.CreateIndex(
                name: "IX_unituser_unitId",
                table: "unituser",
                column: "unitId");

            migrationBuilder.CreateIndex(
                name: "IX_unituser_userId",
                table: "unituser",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_uploadfile_uploadedBy",
                table: "uploadfile",
                column: "uploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_user_orgId",
                table: "user",
                column: "orgId");

            migrationBuilder.CreateIndex(
                name: "IX_user_positionId",
                table: "user",
                column: "positionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_unitId",
                table: "user",
                column: "unitId");

            migrationBuilder.CreateIndex(
                name: "IX_user_unitUserId",
                table: "user",
                column: "unitUserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_userParent",
                table: "user",
                column: "userParent");

            migrationBuilder.CreateIndex(
                name: "UQ_user_email",
                table: "user",
                column: "email",
                unique: true,
                filter: "[email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_user_username",
                table: "user",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_skill_skillId",
                table: "user_skill",
                column: "skillId");

            migrationBuilder.CreateIndex(
                name: "IX_user_skill_userId",
                table: "user_skill",
                column: "userId");

            migrationBuilder.CreateIndex(
                name: "IX_user_skill_verifiedBy",
                table: "user_skill",
                column: "verifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_userrole_roleid",
                table: "userrole",
                column: "roleid");

            migrationBuilder.CreateIndex(
                name: "IX_userrole_userid",
                table: "userrole",
                column: "userid");

            migrationBuilder.CreateIndex(
                name: "IX_workload_metric_userId",
                table: "workload_metric",
                column: "userId");

            migrationBuilder.AddForeignKey(
                name: "FK_agent_context_uploadfile",
                table: "agent_context",
                column: "fileId",
                principalTable: "uploadfile",
                principalColumn: "fileId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentHistory_AssignedBy",
                table: "assignment_history",
                column: "assignedByUserId",
                principalTable: "user",
                principalColumn: "userId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentHistory_AssignedTo",
                table: "assignment_history",
                column: "assignedToUserId",
                principalTable: "user",
                principalColumn: "userId");

            migrationBuilder.AddForeignKey(
                name: "FK_AssignmentHistory_Task",
                table: "assignment_history",
                column: "taskId",
                principalTable: "task",
                principalColumn: "taskId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeCluster_User",
                table: "employee_cluster",
                column: "userId",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeProfile_User",
                table: "employee_profile",
                column: "userId",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fkNotificationTask",
                table: "notification",
                column: "taskId",
                principalTable: "task",
                principalColumn: "taskId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fkNotificationUser",
                table: "notification",
                column: "userId",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceReview_ReviewedBy",
                table: "performance_review",
                column: "reviewedByUserId",
                principalTable: "user",
                principalColumn: "userId");

            migrationBuilder.AddForeignKey(
                name: "FK_PerformanceReview_User",
                table: "performance_review",
                column: "userId",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fkProgressTask",
                table: "progress",
                column: "taskId",
                principalTable: "task",
                principalColumn: "taskId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fkProgressUpdatedBy",
                table: "progress",
                column: "updatedBy",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_reminder_User_UserId",
                table: "reminder",
                column: "UserId",
                principalTable: "user",
                principalColumn: "userId");

            migrationBuilder.AddForeignKey(
                name: "reminder_ibfk_3",
                table: "reminder",
                column: "createdby",
                principalTable: "user",
                principalColumn: "userId");

            migrationBuilder.AddForeignKey(
                name: "reminder_ibfk_1",
                table: "reminder",
                column: "taskid",
                principalTable: "task",
                principalColumn: "taskId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportReview_User",
                table: "report_review",
                column: "reviewerId",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fkReportCreatedBy",
                table: "reportsummary",
                column: "createdBy",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fkReportFile",
                table: "reportsummary",
                column: "reportFile",
                principalTable: "uploadfile",
                principalColumn: "fileId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fkReportTask",
                table: "reportsummary",
                column: "taskId",
                principalTable: "task",
                principalColumn: "taskId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fkTaskAssignee",
                table: "task",
                column: "assigneeId",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "taskassignees_ibfk_2",
                table: "taskassignees",
                column: "UserId",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fkUnitUserUser",
                table: "unituser",
                column: "userId",
                principalTable: "user",
                principalColumn: "userId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fkUnitUserUser",
                table: "unituser");

            migrationBuilder.DropTable(
                name: "agent_context");

            migrationBuilder.DropTable(
                name: "assignment_history");

            migrationBuilder.DropTable(
                name: "employee_cluster");

            migrationBuilder.DropTable(
                name: "employee_profile");

            migrationBuilder.DropTable(
                name: "frequency_detail");

            migrationBuilder.DropTable(
                name: "performance_review");

            migrationBuilder.DropTable(
                name: "reminderunit");

            migrationBuilder.DropTable(
                name: "report_review");

            migrationBuilder.DropTable(
                name: "reportsummary");

            migrationBuilder.DropTable(
                name: "task_skill_requirement");

            migrationBuilder.DropTable(
                name: "taskassignees");

            migrationBuilder.DropTable(
                name: "taskunitassignment");

            migrationBuilder.DropTable(
                name: "user_skill");

            migrationBuilder.DropTable(
                name: "userrole");

            migrationBuilder.DropTable(
                name: "workload_metric");

            migrationBuilder.DropTable(
                name: "reminder");

            migrationBuilder.DropTable(
                name: "progress");

            migrationBuilder.DropTable(
                name: "uploadfile");

            migrationBuilder.DropTable(
                name: "skill");

            migrationBuilder.DropTable(
                name: "role");

            migrationBuilder.DropTable(
                name: "notification");

            migrationBuilder.DropTable(
                name: "period");

            migrationBuilder.DropTable(
                name: "task");

            migrationBuilder.DropTable(
                name: "frequency");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "unituser");

            migrationBuilder.DropTable(
                name: "position");

            migrationBuilder.DropTable(
                name: "unit");

            migrationBuilder.DropTable(
                name: "org");
        }
    }
}
