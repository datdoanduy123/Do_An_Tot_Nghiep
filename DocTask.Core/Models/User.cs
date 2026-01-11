namespace DocTask.Core.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public int? OrgId { get; set; }

    public int? UnitId { get; set; }

    public int? UserParent { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? Refreshtoken { get; set; }
    public DateTime? Refreshtokenexpirytime { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
    public string Role { get; set; } = null!;

    public int? UnitUserId { get; set; }

    public int? PositionId { get; set; }

    public string? PositionName { get; set; }

    public virtual ICollection<User> InverseUserParentNavigation { get; set; } = new List<User>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual Org? Org { get; set; }

    public virtual Position? Position { get; set; }

    public virtual ICollection<Progress> Progresses { get; set; } = new List<Progress>();

    public virtual ICollection<Reminder> ReminderCreatedbyNavigations { get; set; } = new List<Reminder>();

    public virtual ICollection<Reminder> ReminderUsers { get; set; } = new List<Reminder>();

    public virtual ICollection<ReportReview> ReportReviews { get; set; } = new List<ReportReview>();

    public virtual ICollection<Reportsummary> Reportsummaries { get; set; } = new List<Reportsummary>();

    public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

    public virtual Unit? Unit { get; set; }

    public virtual Unituser? UnitUser { get; set; }

    public virtual ICollection<Unituser> Unitusers { get; set; } = new List<Unituser>();

    public virtual ICollection<Uploadfile> Uploadfiles { get; set; } = new List<Uploadfile>();

    public virtual User? UserParentNavigation { get; set; }

    public virtual ICollection<Userrole> Userroles { get; set; } = new List<Userrole>();

    public virtual ICollection<Task> TasksNavigation { get; set; } = new List<Task>();
    
    // Navigation properties mới cho K-means và AI features
    public virtual EmployeeProfile? EmployeeProfile { get; set; }
    
    public virtual EmployeeCluster? EmployeeCluster { get; set; }
    
    public virtual ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    
    public virtual ICollection<WorkloadMetric> WorkloadMetrics { get; set; } = new List<WorkloadMetric>();
    
    public virtual ICollection<AssignmentHistory> AssignmentsReceived { get; set; } = new List<AssignmentHistory>();
    
    public virtual ICollection<AssignmentHistory> AssignmentsGiven { get; set; } = new List<AssignmentHistory>();
    
    public virtual ICollection<PerformanceReview> PerformanceReviews { get; set; } = new List<PerformanceReview>();
    
    public virtual ICollection<PerformanceReview> PerformanceReviewsGiven { get; set; } = new List<PerformanceReview>();
}

