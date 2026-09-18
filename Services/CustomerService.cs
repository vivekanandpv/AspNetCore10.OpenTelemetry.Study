using System.Diagnostics;
using AspNetCore10.OpenTelemetry.Study.Config;
using AspNetCore10.OpenTelemetry.Study.DAL;
using AspNetCore10.OpenTelemetry.Study.DTOs;
using AspNetCore10.OpenTelemetry.Study.Exceptions;
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
        using var activity = Telemetry.ActivitySource.StartActivity("customer.get_all");
        var stopwatch = Stopwatch.StartNew();

        var customers = await _context.Customers
            .Select(c => new CustomerDto(c.Id, c.Name, c.Email, c.PhoneNumber))
            .ToListAsync();

        RecordDuration("get_all", stopwatch.Elapsed.TotalMilliseconds);
        return customers;
    }

    public async Task<CustomerDto?> GetCustomerByIdAsync(int id)
    {
        _logger.LogInformation("Searching for customer with ID: {CustomerId}.", id);
        using var activity = Telemetry.ActivitySource.StartActivity("customer.get_by_id");
        activity?.SetTag("customer.id", id);
        var stopwatch = Stopwatch.StartNew();

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            _logger.LogWarning("Customer with ID: {CustomerId} was not found.", id);
            RecordDuration("get_by_id", stopwatch.Elapsed.TotalMilliseconds, found: false);
            return null;
        }

        RecordDuration("get_by_id", stopwatch.Elapsed.TotalMilliseconds);
        return new CustomerDto(customer.Id, customer.Name, customer.Email, customer.PhoneNumber);
    }

    public async Task<CustomerDto> CreateCustomerAsync(CreateCustomerDto createCustomerDto)
    {
        _logger.LogInformation("Creating a new customer with Email: {CustomerEmail}.", createCustomerDto.Email);

        using var activity = Telemetry.ActivitySource.StartActivity("customer.create");
        activity?.AddTag("customer.email", createCustomerDto.Email);
        activity?.AddTag("customer.phone_number", createCustomerDto.PhoneNumber);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            activity?.AddEvent(new ActivityEvent("customer.validation.start"));

            var emailTaken = await _context.Customers
                .AnyAsync(c => c.Email == createCustomerDto.Email);
            if (emailTaken)
            {
                _logger.LogWarning("Failed to create customer. Email: {CustomerEmail} is already in use.", createCustomerDto.Email);
                throw new DuplicateEmailException(createCustomerDto.Email);
            }

            var customer = new Customer
            {
                Name = createCustomerDto.Name,
                Email = createCustomerDto.Email,
                PhoneNumber = createCustomerDto.PhoneNumber
            };

            activity?.AddEvent(new ActivityEvent("customer.validation.done"));

            _context.Customers.Add(customer);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A concurrent request may have won the race to insert this email
                // between our pre-check above and this SaveChanges — the unique
                // index is the real guarantee, the AnyAsync check above is just
                // the common-case fast path.
                var raceLost = await _context.Customers
                    .AnyAsync(c => c.Email == createCustomerDto.Email && c.Id != customer.Id);
                if (!raceLost)
                {
                    throw;
                }

                throw new DuplicateEmailException(createCustomerDto.Email);
            }

            activity?.SetTag("customer.id", customer.Id);
            Telemetry.CustomersCreated.Add(1);
            RecordDuration("create", stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogInformation("Successfully created customer with ID: {CustomerId}.", customer.Id);
            return new CustomerDto(customer.Id, customer.Name, customer.Email, customer.PhoneNumber);
        }
        catch (Exception e)
        {
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);
            activity?.AddException(e);
            RecordDuration("create", stopwatch.Elapsed.TotalMilliseconds, success: false);
            throw;
        }
    }

    public async Task<bool> UpdateCustomerAsync(UpdateCustomerDto updateCustomerDto)
    {
        _logger.LogInformation("Updating customer with ID: {CustomerId}.", updateCustomerDto.Id);
        using var activity = Telemetry.ActivitySource.StartActivity("customer.update");
        activity?.SetTag("customer.id", updateCustomerDto.Id);
        var stopwatch = Stopwatch.StartNew();

        var customer = await _context.Customers.FindAsync(updateCustomerDto.Id);
        if (customer == null)
        {
            _logger.LogWarning("Failed to update customer. Customer with ID: {CustomerId} was not found.", updateCustomerDto.Id);
            RecordDuration("update", stopwatch.Elapsed.TotalMilliseconds, found: false);
            return false;
        }

        customer.Name = updateCustomerDto.Name;
        customer.Email = updateCustomerDto.Email;
        customer.PhoneNumber = updateCustomerDto.PhoneNumber;

        await _context.SaveChangesAsync();
        RecordDuration("update", stopwatch.Elapsed.TotalMilliseconds);
        _logger.LogInformation("Successfully updated customer with ID: {CustomerId}.", updateCustomerDto.Id);
        return true;
    }

    public async Task<bool> DeleteCustomerAsync(int id)
    {
        _logger.LogInformation("Deleting customer with ID: {CustomerId}.", id);
        using var activity = Telemetry.ActivitySource.StartActivity("customer.delete");
        activity?.SetTag("customer.id", id);
        var stopwatch = Stopwatch.StartNew();

        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            _logger.LogWarning("Failed to delete customer. Customer with ID: {CustomerId} was not found.", id);
            RecordDuration("delete", stopwatch.Elapsed.TotalMilliseconds, found: false);
            return false;
        }

        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        Telemetry.CustomersDeleted.Add(1);
        RecordDuration("delete", stopwatch.Elapsed.TotalMilliseconds);
        _logger.LogInformation("Successfully deleted customer with ID: {CustomerId}.", id);
        return true;
    }

    private static void RecordDuration(string operation, double elapsedMs, bool success = true, bool found = true)
    {
        Telemetry.CustomerOperationDuration.Record(
            elapsedMs,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("success", success),
            new KeyValuePair<string, object?>("found", found));
    }
}
