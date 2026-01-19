namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Odpowiedü z Przelewy24 po øπdaniu zwrotu
    /// </summary>
    public record RefundResponseDto
    {
        public RefundData? Data { get; init; }
    }

    public record RefundData
    {
        /// <summary>
        /// UUID zwrotu
        /// </summary>
        public string? RefundsUuid { get; init; }
        
        /// <summary>
        /// Status zwrotu
        /// </summary>
        public string? Status { get; init; }
    }
}