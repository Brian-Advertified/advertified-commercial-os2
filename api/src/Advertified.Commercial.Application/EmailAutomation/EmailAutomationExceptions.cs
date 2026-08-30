namespace Advertified.Commercial.Application.EmailAutomation;

public sealed class InvalidEmailWebhookException : Exception
{
    public InvalidEmailWebhookException()
        : base("The inbound email notification could not be verified.")
    {
    }
}

public sealed class InboundMailboxNotConfiguredException : Exception
{
    public InboundMailboxNotConfiguredException()
        : base("The recipient mailbox is not configured for proposal automation.")
    {
    }
}

public sealed class EmailAutomationReviewRequiredException : Exception
{
    public EmailAutomationReviewRequiredException(string failureCode, string message)
        : base(message)
    {
        FailureCode = failureCode;
    }

    public string FailureCode { get; }
}

public sealed class EmailAutomationNotRetryableException : Exception
{
    public EmailAutomationNotRetryableException()
        : base("This inbound proposal run cannot be retried from its current state.")
    {
    }
}

public sealed class EmailAttachmentBlockedException : Exception
{
    public EmailAttachmentBlockedException()
        : base("An inbound email attachment requires review before automation may continue.")
    {
    }
}

public sealed class EmailPayloadUnavailableException : Exception
{
    public EmailPayloadUnavailableException(Exception? innerException = null)
        : base("The complete inbound email payload is unavailable.", innerException)
    {
    }
}

public sealed class EmailProviderUnavailableException : Exception
{
    public EmailProviderUnavailableException(Exception? innerException = null)
        : base("The configured email provider is unavailable.", innerException)
    {
    }
}

public sealed class EmailDeliveryFailedException : Exception
{
    public EmailDeliveryFailedException(Exception? innerException = null)
        : base("The proposal email could not be delivered.", innerException)
    {
    }
}
