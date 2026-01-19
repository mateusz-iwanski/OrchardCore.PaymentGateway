using System.Collections.Generic;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Objects
{
    /// <summary>
    /// Lista dostêpnych metod p³atnoœci z Przelewy24
    /// </summary>
    public record PaymentMethodsResponseDto
    {
        public List<PaymentMethod>? Data { get; init; }
    }

    public record PaymentMethod
    {
        public int Id { get; init; }
        public string? Name { get; init; }
        public string? ImgUrl { get; init; }
        public bool Status { get; init; }
        public bool Mobile { get; init; }
    }
}