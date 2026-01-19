using OrchardCore.PaymentGateway.Providers.Przelewy24.Objects;
using Refit;
using System.Threading.Tasks;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Clients
{
    public interface IPrzelewy24Api
    {
        [Post("/transaction/register")]
        Task<RegisterResponseDto> RegisterAsync([Body(BodySerializationMethod.UrlEncoded)] RegisterRequestDto request);
    }
}
