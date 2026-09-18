namespace AspNetCore10.OpenTelemetry.Study.Exceptions;

// The message deliberately excludes the email itself: it ends up in the
// Activity's status/exception event (via SetStatus/AddException), which the
// collector's redaction processor can't reach — it only masks span
// attributes, not span events or the status message. Keeping PII out of the
// message is the actual fix; the Email property is still there for the
// controller to use in the HTTP response, which is going back to the same
// caller who submitted it.
public class DuplicateEmailException(string email)
    : Exception("A customer with this email already exists.")
{
    public string Email { get; } = email;
}
