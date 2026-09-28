using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AIEmployeeLeaveManagement.Models
{
    public class LeaveRequest : IValidatableObject
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Employee name is required")]
        [StringLength(50)]
        [Display(Name = "Employee Name")]
        public string EmployeeName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Department is required")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select a leave type")]
        [Display(Name = "Leave Type")]
        public string LeaveType { get; set; } = string.Empty;

        [Required, DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required, DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.Today;

        [Required, StringLength(200)]
        public string Reason { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";
        public DateTime AppliedOn { get; set; } = DateTime.Now;

        [NotMapped]
        [Display(Name = "Total Days")]
        public int TotalDays => (EndDate.Date - StartDate.Date).Days + 1;

        // Custom validation
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            if (EndDate < StartDate)
                yield return new ValidationResult(
                    "End date cannot be before start date", new[] { nameof(EndDate) });
        }
    }
}