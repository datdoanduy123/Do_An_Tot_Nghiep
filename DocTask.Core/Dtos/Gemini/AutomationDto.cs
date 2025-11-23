using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DocTask.Core.Dtos.Gemini
{
    // assignedUserIds && assignedUnitIds
    public class CreateTaskAutomationDto
    {
        [Required(ErrorMessage = "Title required", AllowEmptyStrings = false)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Description required", AllowEmptyStrings = false)]
        public string Description { get; set; } = "";

        [Required(ErrorMessage = "Start Date required", AllowEmptyStrings = false)]
        public string StartDate { get; set; } = "";

        [Required(ErrorMessage = "Due Date required", AllowEmptyStrings = false)]
        public string DueDate { get; set; } = "";

        public string Frequency { get; set; } = "";

        public int IntervalValue { get; set; }

        public List<int> Days { get; set; } = [];

        public List<int>? AssignedUserIds { get; set; } = [];

        public List<int>? AssignedUnitIds { get; set; } = [];

        public List<CreateSubTaskAutomationDto> Subtasks { get; set; } = new();
    }

    public class CreateSubTaskAutomationDto
    {
        [Required(ErrorMessage = "Title required", AllowEmptyStrings = false)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Description required", AllowEmptyStrings = false)]
        public string Description { get; set; } = "";

        [Required(ErrorMessage = "Start Date required", AllowEmptyStrings = false)]
        public string StartDate { get; set; } = "";

        [Required(ErrorMessage = "Due Date required", AllowEmptyStrings = false)]
        public string DueDate { get; set; } = "";

        [Required(ErrorMessage = "Frequency required")]
        public string Frequency { get; set; } = "";

        [Required(ErrorMessage = "IntervalValue required")]
        public int IntervalValue { get; set; }

        [Required(ErrorMessage = "Days required")]
        public List<int> Days { get; set; } = [];

        public List<int>? AssignedUserIds { get; set; } = [];

        public List<int>? AssignedUnitIds { get; set; } = [];
    }
}