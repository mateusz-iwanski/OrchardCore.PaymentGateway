using System.Text.Json.Serialization;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Odpowiedź z Przelewy24 po rejestracji transakcji
    /// </summary>
    public record RegisterResponseDto
    {
        public RegisterData? Data { get; init; }
    }

    public record RegisterData
    {
        /// <summary>
        /// Token transakcji do utworzenia URL płatności
        /// Format: https://sandbox.przelewy24.pl/trnRequest/{token}
        /// </summary>
        public string? Token { get; init; }
    }
}
