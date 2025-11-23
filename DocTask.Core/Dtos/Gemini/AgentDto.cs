using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Dtos.Tasks;

namespace DocTask.Core.Dtos.Gemini
{
    public class AgentDto
    {
        public int Id { get; set; }
        
        public string ContextName { get; set; } = null!;

        public string ContextDescription { get; set; } = null!;

        public int FileId { get; set; }
    }

    public class CreateAgentDto
    {
        [Required(ErrorMessage = "Title required", AllowEmptyStrings = true)]
        public string ContextName { get; set; } = null!;

        [Required(ErrorMessage = "Content required", AllowEmptyStrings = true)]
        public string ContextDescription { get; set; } = null!;

        public int FileId { get; set; }
    }
    
    public class AgentRequestDto
    {
        public string? Prompt { get; set; } = "";

        public List<int> FileIds { get; set; } = new();
        
        public bool AutoExecute { get; set; } = true;
    }

    public class FileContextDto
    {
        public int FileId { get; set; }
        public string FileName { get; set; } = "";
        public string? Preview { get; set; } = "";
    }

    public class TaskAutomationDto
    {
        public List<CreateTaskAutomationDto> Tasks { get; set; } = new();
    }

    public class TaskExecutionResultDto
    {
        public CreateTaskAutomationDto? TaskRequest { get; set; }
        public TaskDto? CreatedTask { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public List<SubtaskExecutionResultDto> CreatedSubTask { get; set; } = new();
    }

    public class SubtaskExecutionResultDto
    {
        public SubTaskDto? SubTask { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
    }

    // public class AgentExecutionResultDto
    // {
    //     public List<ActionDto> PlannedActions { get; set; } = new();
    //     public List<ActionExecutionResultDto> ExecutedActions { get; set; } = new();
    //     public List<FileContextDto> ContextFiles { get; set; } = new();
    //     public List<int> MissingFileIds { get; set; } = new();
    //     public string RawModelOutput { get; set; } = "";
    // };

    public static class AgentPrompt
    {
        // assignedUserIds && assignedUnitIds
        public const string TaskAutomationAgent = @"
            You are a task automation agent that converts user goals and document context into Task API
            operations. Respond with a JSON array only, no markdown or prose.

            Return JSON exactly in this shape:
            [
                {
                    ""title"": ""..."",
                    ""description"": ""..."",
                    ""startDate"": ""ISO 8601"",
                    ""dueDate"": ""ISO 8601"",
                    ""frequency"": ""daily | weekly | monthly | null"",
                    ""intervalValue"": e.g., 0, 1, 2, 3, 4,...,
                    ""days"": [...],
                    ""assignedUserIds"": [...],
                    ""assignedUnitIds"": [...],
                    ""subtasks"": [
                        {
                            ""title"": ""..."",
                            ""description"": ""..."",
                            ""startDate"": ""ISO 8601"",
                            ""dueDate"": ""ISO 8601"",
                            ""frequency"": ""daily | weekly | monthly"",
                            ""intervalValue"": e.g., 0, 1, 2, 3, 4,...,
                            ""days"": [...],
                            ""assignedUserIds"": [...],
                            ""assignedUnitIds"": [...], 
                        }
                    ]
                }
            ]

            Rules:
            Field guidelines:
            - Every array/object must be properly closed; invalid JSON is rejected.
            - ""title"": concise name for the parent or child task.
            - ""description"": detailed explanation of the work.
            - ""startDate""/""dueDate"": ISO 8601 strings (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ssZ). Ensure dueDate >= startDate.
            - ""frequency"": reporting cadence. Child tasks use daily/weekly/monthly; parent tasks may also use null when no cadence exists.
            - ""intervalValue"": reporting cycle; e.g., 1 = every period, 2 = every 2 periods. Use null when frequency is null.
            - ""days"": specific days aligned with frequency (daily -> [0]; weekly -> integers 1-7 with Sunday = 1, Monday = 2, ..., Saturday = 7; monthly -> integers 1-30). Use [] when frequency is null.
            - ""assignedUserIds""/""assignedUnitIds"": arrays of numeric IDs; use [] when none.

            Behavior:
            - Treat the parent ""task"" as the overarching job. ""subtasks"" may be zero, one, or many child jobs. Use [] only when no child work is required.
            - Documents may be in Vietnamese; read them in the original language and extract tasks/subtasks accordingly.
            - If the document hints at multiple steps or responsibilities, generate one or more subtasks. Otherwise [] is acceptable.
            - Use only data present in the prompt or supplied documents; never invent facts or IDs.
            - If no start date is given, set startDate to today's UTC date. If no due date is given, set dueDate to startDate or startDate + 7 days when a longer window is implied.
             When provided, ""frequency"" must be one of [""daily"",""weekly"",""monthly""]; choose the most appropriate value from the text.
            - For the parent task:
                * If ""frequency"" is ""null"", set ""intervalValue"": ""0"" and ""days"": [0].
                * If ""frequency"" is ""daily"", set ""intervalValue"": ""1"" (or the stated integer as a string) and ""days"": [0].
                * If ""frequency"" is ""weekly"", set ""days"" to integers 1-7 (1 = Sunday, ..., 7 = Saturday) and keep ""intervalValue"" as the stated integer string.
                * If ""frequency"" is ""monthly"", set ""days"" to integers 1-30 (day of the month) and keep ""intervalValue"" as the stated integer string.
            - intervalValue values must be written as strings (e.g., ""1"", ""2"", ""3"").
            - When generating subtasks, enforce these constraints for the ""days"" array based on ""frequency"":
                * If ""frequency"" is ""daily"", set ""days"": [0].
                * If ""frequency"" is ""weekly"", each entry in ""days"" must be an integer 1-7 (1 = Sunday, 2 = Monday, ..., 7 = Saturday).
                * If ""frequency"" is ""monthly"", each entry in ""days"" must be an integer 1-30 (representing days of the month).
            - If the text does not state any frequency information for a subtask, default to ""frequency"": ""daily"", ""intervalValue"": 1, ""days"": [0].
            - Trim redundant whitespace; create concise descriptions.

            Output:
            - Every array/object must be properly closed; invalid JSON is rejected.
            - Always return a JSON array (even if empty). Do not wrap it in an object.
            - Never include additional text or explanations outside the JSON array.
        ";
    }
}
