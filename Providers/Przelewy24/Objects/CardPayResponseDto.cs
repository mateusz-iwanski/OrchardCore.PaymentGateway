namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Odpowiedü z Przelewy24 po p≥atnoúci kartπ
    /// </summary>
    public record CardPayResponseDto
    {
        public CardPayData? Data { get; init; }
    }

    public record CardPayData
    {
        public int OrderId { get; init; }
        public string? Status { get; init; }
    }
}