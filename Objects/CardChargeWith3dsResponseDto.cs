namespace ApiClientPrzelewy24.Objects
{
    /// <summary>
    /// Odpowiedü z Przelewy24 po obciπøeniu karty z 3DS
    /// </summary>
    public record CardChargeWith3dsResponseDto
    {
        public CardChargeWith3dsData? Data { get; init; }
    }

    public record CardChargeWith3dsData
    {
        public string? RedirectUrl { get; init; }
        public int OrderId { get; init; }
    }
}