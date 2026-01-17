namespace ApiClientPrzelewy24.Objects
{
    /// <summary>
    /// ¯¹danie p³atnoœci aliasem BLIK (1-click)
    /// </summary>
    public record BlikChargeByAliasRequestDto(
        int MerchantId,
        int PosId,
        string SessionId,
        int Amount,
        string Currency,
        string Description,
        string Email,
        string BlikAliasValue,
        string BlikAliasLabel
    )
    {
        public string? UrlReturn { get; init; }
        public string? UrlStatus { get; init; }
        public string? Sign { get; init; }
    }
}