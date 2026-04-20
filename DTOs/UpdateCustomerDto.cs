using System.ComponentModel.DataAnnotations;

namespace AspNetCore10.OpenTelemetry.Study.DTOs;

public record UpdateCustomerDto(
    [Required] int Id,
    [Required] [StringLength(100)] string Name,
    [Required] [EmailAddress] string Email,
    [Phone] string PhoneNumber);
