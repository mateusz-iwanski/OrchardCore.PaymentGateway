namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Szczegó³y transakcji z Przelewy24
    /// </summary>
    public record TransactionDetailsDto
    {
        public TransactionData? Data { get; init; }
    }

    public record TransactionData
    {
        public int OrderId { get; init; }
        public string? SessionId { get; init; }
        public int Amount { get; init; }
        public string? Currency { get; init; }
        public string? Email { get; init; }
        public string? Description { get; init; }
        public int MethodId { get; init; }
        public string? Statement { get; init; }
        public string? Status { get; init; }
    }
}