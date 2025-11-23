using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocTask.Core.Dtos.Reminders
{
    public class CreateReminderUnitRequest
    {
        [Required(ErrorMessage = "Nội dung là bắt buộc")]
        [StringLength(1000, ErrorMessage = "Nội dung không được vượt quá 1000 ký tự")]
        public string Message { get; set; } = null!;
    }
}
