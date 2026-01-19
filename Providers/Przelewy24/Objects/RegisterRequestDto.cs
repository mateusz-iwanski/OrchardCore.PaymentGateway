using System.Text.Json.Serialization;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    // DTO zgodne z dokumentacją Przelewy24 reprezentujące parametry wymagane przy rejestracji transakcji.
    // Każde pole odpowiada jednej pozycji formularza wysyłanego do endpointu `transaction/register`.
    // Komentarze przy polach opisują znaczenie, format i ewentualne ograniczenia/uwagi.
    public sealed record RegisterRequestDto(
        // Identyfikator merchantski przypisany przez Przelewy24 (liczba całkowita).
        // Przykład: 12345. W dokumentacji czasem używane zamiennie z `posId` — zależnie od konfiguracji konta.
        [property: JsonPropertyName("merchantId")] int MerchantId,

        // Identyfikator punktu sprzedaży (POS). Często jest taki sam jak `merchantId`, ale może się różnić.
        // Typ: całkowity. Przykład: 12345.
        [property: JsonPropertyName("posId")] int PosId,

        // Kwota transakcji w najniższej jednostce waluty (grosze dla PLN).
        // Typ: long, ponieważ kwoty mogą być duże. Przykład: 1000 => 10.00 PLN.
        [property: JsonPropertyName("amount")] long Amount,

        // Trzyznakowy kod waluty zgodny z ISO 4217 (domyślnie "PLN").
        // Powinno być zawsze wielkimi literami. Przykład: "PLN".
        [property: JsonPropertyName("currency")] string Currency = "PLN",

        // Krótki opis transakcji widoczny dla klienta i w panelu Przelewy24.
        // Maksymalna długość/polityka zawartości zależy od dokumentacji Przelewy24 — najlepiej nie przesadzać z długością.
        [property: JsonPropertyName("description")] string Description = "",

        // Adres e-mail płatnika. Opcjonalnie używany do powiadomień lub weryfikacji.
        // Powinien być poprawnym adresem e-mail. Przykład: "test@example.com".
        [property: JsonPropertyName("email")] string Email = "",

        // Kod kraju ISO 3166-1 alpha-2 (np. "PL"). Używany przy walidacjach i lokalizacji.
        [property: JsonPropertyName("country")] string Country = "PL",

        // Kod języka (np. "pl" lub "en"). Określa język komunikatów/wyświetlania na stronie płatności.
        [property: JsonPropertyName("language")] string Language = "pl",

        // URL, na który użytkownik zostanie przekierowany po zakończeniu płatności (success/failure).
        // Musi być pełnym adresem (z protokołem). Przykład: "https://example.com/return".
        [property: JsonPropertyName("urlReturn")] string UrlReturn = "",

        // URL statusu (webhook/status) — Przelewy24 wyśle tam aktualizacje statusu płatności.
        // Musi być dostępny z internetu (dla sandbox/testów można użyć narzędzi typu ngrok) i obsługiwać POST.
        [property: JsonPropertyName("urlStatus")] string UrlStatus = "",

        // Unikalny identyfikator sesji partnera — obowiązkowy przy wyliczaniu `sign`.
        // Powinien być unikalny dla każdej rejestracji transakcji (np. GUID bez myślników).
        // Używany do powiązania transakcji po stronie klienta z odpowiedzią/notify.
        [property: JsonPropertyName("sessionId")] string SessionId = "",

        // Pole z podpisem (sign) obliczanym lokalnie przez serwis przed wysłaniem żądania.
        // Nie jest to pole wypełniane przez użytkownika końcowego — serwis powinien je ustawić na podstawie
        // wartości pól (np. merchantId, posId, sessionId, amount) oraz klucza CRC/secret zgodnie z dokumentacją P24.
        // Przykład wartości: sha384/sha256/hex-encoded string zależnie od algorytmu używanego w providerze.
        [property: JsonPropertyName("sign")] string Sign = ""
    );
}
