namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// DTO dla weryfikacji transakcji w Przelewy24.
    /// U¿ywany w endpoint PUT /transaction/verify
    /// </summary>
    public record VerifyRequestDto(
        int MerchantId,
        int PosId,
        string SessionId,
        int Amount,
        string Currency,
        int OrderId,
        string Sign
    );
}