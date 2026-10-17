using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Contracts;

public class SetWorkingHoursRequest : IValidatableObject
{
    [Required]
    public TimeOnly? StartTime { get; set; }

    [Required]
    public TimeOnly? EndTime { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartTime.HasValue && EndTime.HasValue && StartTime.Value >= EndTime.Value)
            yield return new ValidationResult("EndTime must be later than StartTime.", new[] { nameof(EndTime) });
    }
}
