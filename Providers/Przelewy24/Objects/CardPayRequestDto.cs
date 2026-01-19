namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// ¯¹danie p³atnoœci kart¹ z bezpoœrednimi danymi (wymaga PCI DSS)
    /// </summary>
    public record CardPayRequestDto(
        string CardNumber,
        string CardDate,
        string Cvv,
        int MerchantId,
        int PosId,
        string SessionId,
        int Amount,
        string Currency,
        string Description,
        string Email,
        string UrlReturn,
        string UrlStatus
    )
    {
        public string? Country { get; init; }
        public string? Language { get; init; }
        public string? Sign { get; init; }
    }
}