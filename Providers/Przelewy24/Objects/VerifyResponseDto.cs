namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Odpowiedü z Przelewy24 po weryfikacji transakcji
    /// </summary>
    public record VerifyResponseDto
    {
        public VerifyData? Data { get; init; }
    }

    public record VerifyData
    {
        /// <summary>
        /// Status weryfikacji (np. "success")
        /// </summary>
        public string? Status { get; init; }
    }
}