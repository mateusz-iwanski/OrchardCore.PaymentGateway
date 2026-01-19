namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Szczegó³y zwrotu z Przelewy24
    /// </summary>
    public record RefundDetailsDto
    {
        public RefundDetailsData? Data { get; init; }
    }

    public record RefundDetailsData
    {
        public int OrderId { get; init; }
        public string? SessionId { get; init; }
        public int Amount { get; init; }
        public string? Currency { get; init; }
        public string? Status { get; init; }
        public string? RefundsUuid { get; init; }
        public string? CreatedAt { get; init; }
    }
}