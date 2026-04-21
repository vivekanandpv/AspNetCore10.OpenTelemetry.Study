using System.Diagnostics;
using AspNetCore10.OpenTelemetry.Study.Config;
using AspNetCore10.OpenTelemetry.Study.DAL;
using AspNetCore10.OpenTelemetry.Study.DTOs;
using AspNetCore10.OpenTelemetry.Study.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AspNetCore10.OpenTelemetry.Study.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(AppDbContext context, ILogger<CustomerService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<CustomerDto>> GetAllCustomersAsync()
    {
        _logger.LogInformation("Getting all customers from the database.");
        return await _context.Customers
            .Select(c => new CustomerDto(c.Id, c.Name, c.Email, c.PhoneNumber))
            .ToListAsync();
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(int id)
    {
        _logger.LogInformation("Searching for customer with ID: {CustomerId}.", id);
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            _logger.LogWarning("Customer with ID: {CustomerId} was not found.", id);
            return null;
        }

        return new CustomerDto(customer.Id, customer.Name, customer.Email, customer.PhoneNumber);
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto createCustomerDto)
    {
        _logger.LogInformation("Creating a new customer with Email: {CustomerEmail}.", createCustomerDto.Email);
        
        using var activity = Telemetry.ActivitySource.StartActivity("customer.create");
        activity?.AddTag("customer.email", createCustomerDto.Email);

        try
        {
            activity?.AddEvent(new ActivityEvent("customer.validation.start"));
            var customer = new Customer
            {
                Name = createCustomerDto.Name,
                Email = createCustomerDto.Email,
                PhoneNumber = createCustomerDto.PhoneNumber
            };
            
            activity?.AddEvent(new ActivityEvent("customer.validation.done"));

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            
            activity?.SetTag("customer.id", customer.Id);
            Telemetry.CustomersCreated.Add(1);
            
            _logger.LogInformation("Successfully created customer with ID: {CustomerId}.", customer.Id);
            return new CustomerDto(customer.Id, customer.Name, customer.Email, customer.PhoneNumber);
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.AddException(e);
            throw;
        }
    }

    public async Task<bool> UpdateCustomerAsync(UpdateCustomerDto updateCustomerDto)
    {
        _logger.LogInformation("Updating customer with ID: {CustomerId}.", updateCustomerDto.Id);
        var customer = await _context.Customers.FindAsync(updateCustomerDto.Id);
        if (customer == null)
        {
            _logger.LogWarning("Failed to update customer. Customer with ID: {CustomerId} was not found.", updateCustomerDto.Id);
            return false;
        }

        customer.Name = updateCustomerDto.Name;
        customer.Email = updateCustomerDto.Email;
        customer.PhoneNumber = updateCustomerDto.PhoneNumber;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully updated customer with ID: {CustomerId}.", updateCustomerDto.Id);
        return true;
    }

    public async Task<bool> DeleteCustomerAsync(int id)
    {
        _logger.LogInformation("Deleting customer with ID: {CustomerId}.", id);
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            _logger.LogWarning("Failed to delete customer. Customer with ID: {CustomerId} was not found.", id);
            return false;
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Successfully deleted customer with ID: {CustomerId}.", id);
        return true;
    }
}
