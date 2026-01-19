namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Informacje o karcie z Przelewy24
    /// </summary>
    public record CardInfoResponseDto
    {
        public CardInfoData? Data { get; init; }
    }

    public record CardInfoData
    {
        public string? RefId { get; init; }
        public string? Mask { get; init; }
        public string? Bin { get; init; }
    }
}