using System.Collections.Generic;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Historia transakcji, zwrotów i batch z Przelewy24
    /// </summary>
    public record ReportHistoryResponseDto
    {
        public ReportHistoryData? Data { get; init; }
    }

    public record ReportHistoryData
    {
        public List<ReportItem>? Items { get; init; }
    }

    public record ReportItem
    {
        public string? Type { get; init; }
        public int Id { get; init; }
        public string? Date { get; init; }
        public int Amount { get; init; }
        public string? Currency { get; init; }
        public string? Status { get; init; }
    }
}