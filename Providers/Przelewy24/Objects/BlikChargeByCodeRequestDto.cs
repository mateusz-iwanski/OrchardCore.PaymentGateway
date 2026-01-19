namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// ¯¹danie p³atnoœci kodem BLIK (6-cyfrowy kod)
    /// </summary>
    public record BlikChargeByCodeRequestDto(
        int MerchantId,
        int PosId,
        string SessionId,
        int Amount,
        string Currency,
        string Description,
        string Email,
        string BlikCode
    )
    {
        public string? UrlReturn { get; init; }
        public string? UrlStatus { get; init; }
        public string? Sign { get; init; }
    }
}