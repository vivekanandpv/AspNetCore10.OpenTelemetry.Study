using AspNetCore10.OpenTelemetry.Study.DTOs;
using AspNetCore10.OpenTelemetry.Study.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AspNetCore10.OpenTelemetry.Study.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(ICustomerService customerService, ILogger<CustomersController> logger)
    {
        _customerService = customerService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetAll()
    {
        _logger.LogInformation("HTTP GET request for all customers received.");
        var customers = await _customerService.GetAllCustomersAsync();
        return Ok(customers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> GetById(int id)
    {
        _logger.LogInformation("HTTP GET request for customer with ID: {CustomerId} received.", id);
        var customer = await _customerService.GetCustomerByIdAsync(id);
        if (customer == null)
        {
            _logger.LogWarning("HTTP GET request failed. Customer with ID: {CustomerId} was not found.", id);
            return NotFound();
        }
        return Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerDto createCustomerDto)
    {
        _logger.LogInformation("HTTP POST request to create customer received for Email: {CustomerEmail}.", createCustomerDto.Email);
        var customer = await _customerService.CreateCustomerAsync(createCustomerDto);
        _logger.LogInformation("HTTP POST request succeeded. Created customer with ID: {CustomerId}.", customer.Id);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateCustomerDto updateCustomerDto)
    {
        _logger.LogInformation("HTTP PUT request to update customer with ID: {CustomerId} received.", id);
        if (id != updateCustomerDto.Id)
        {
            _logger.LogWarning("HTTP PUT request failed due to ID mismatch. Route ID: {RouteId}, Body ID: {BodyId}.", id, updateCustomerDto.Id);
            return BadRequest("ID mismatch");
        }

        var updated = await _customerService.UpdateCustomerAsync(updateCustomerDto);
        if (!updated)
        {
            _logger.LogWarning("HTTP PUT request failed. Customer with ID: {CustomerId} was not found for update.", id);
            return NotFound();
        }

        _logger.LogInformation("HTTP PUT request succeeded. Updated customer with ID: {CustomerId}.", id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        _logger.LogInformation("HTTP DELETE request for customer with ID: {CustomerId} received.", id);
        var deleted = await _customerService.DeleteCustomerAsync(id);
        if (!deleted)
        {
            _logger.LogWarning("HTTP DELETE request failed. Customer with ID: {CustomerId} was not found for deletion.", id);
            return NotFound();
        }

        _logger.LogInformation("HTTP DELETE request succeeded. Deleted customer with ID: {CustomerId}.", id);
        return NoContent();
    }
}
