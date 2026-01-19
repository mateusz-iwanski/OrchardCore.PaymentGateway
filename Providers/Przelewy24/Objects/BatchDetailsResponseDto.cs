using System.Collections.Generic;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Szczegó³y paczki (batch) z Przelewy24
    /// </summary>
    public record BatchDetailsResponseDto
    {
        public BatchDetailsData? Data { get; init; }
    }

    public record BatchDetailsData
    {
        public int BatchId { get; init; }
        public string? Date { get; init; }
        public List<BatchTransaction>? Transactions { get; init; }
        public List<BatchRefund>? Refunds { get; init; }
    }

    public record BatchTransaction
    {
        public int OrderId { get; init; }
        public string? SessionId { get; init; }
        public int Amount { get; init; }
        public string? Currency { get; init; }
        public string? Status { get; init; }
    }

    public record BatchRefund
    {
        public int OrderId { get; init; }
        public int Amount { get; init; }
        public string? Currency { get; init; }
        public string? Status { get; init; }
    }
}