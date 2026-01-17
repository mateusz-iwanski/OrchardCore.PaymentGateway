namespace ApiClientPrzelewy24.Objects
{
    /// <summary>
    /// Odpowiedü z Przelewy24 po obciπøeniu karty rekurencyjnie (bez 3DS)
    /// </summary>
    public record CardChargeResponseDto
    {
        public CardChargeData? Data { get; init; }
    }

    public record CardChargeData
    {
        public int OrderId { get; init; }
        public string? Status { get; init; }
    }
}