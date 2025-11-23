using System.ComponentModel.DataAnnotations;

namespace DocTask.Core.Dtos.Authentications;

public class ChangePasswordRequestDto
{
    [Required(AllowEmptyStrings = false)]
    public string OldPassword { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [Length(6, 100, ErrorMessage = "Password must be between 6 and 100 characters")]
    public string NewPassword { get; set; } = string.Empty;
}


