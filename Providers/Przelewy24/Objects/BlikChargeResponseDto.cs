namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Odpowiedü z Przelewy24 po p≥atnoúci BLIK
    /// </summary>
    public record BlikChargeResponseDto
    {
        public BlikChargeData? Data { get; init; }
    }

    public record BlikChargeData
    {
        public int OrderId { get; init; }
        public string? Status { get; init; }
    }
}