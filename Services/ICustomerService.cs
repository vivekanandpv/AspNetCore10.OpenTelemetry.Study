using AspNetCore10.OpenTelemetry.Study.DTOs;

namespace AspNetCore10.OpenTelemetry.Study.Services;

public interface ICustomerService
{
    Task<IEnumerable<CustomerDto>> GetAllCustomersAsync();
    Task<CustomerDto?> GetCustomerByIdAsync(int id);
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto createCustomerDto);
    Task<bool> UpdateCustomerAsync(UpdateCustomerDto updateCustomerDto);
    Task<bool> DeleteCustomerAsync(int id);
}
