using System.ComponentModel.DataAnnotations;

namespace RefApp.Validation;

public class UnavailabilityDateRangeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var instance = validationContext.ObjectInstance;
        var startProperty = instance.GetType().GetProperty("StartDate");
        var endProperty = instance.GetType().GetProperty("EndDate");
        if (startProperty == null || endProperty == null)
            return ValidationResult.Success;

        var start = startProperty.GetValue(instance) as DateTime?;
        var end = endProperty.GetValue(instance) as DateTime?;
        if (start == null || end == null)
            return ValidationResult.Success;

        if (end.Value.Date < start.Value.Date)
            return new ValidationResult("End date must be on or after start date.");

        return ValidationResult.Success;
    }
}
