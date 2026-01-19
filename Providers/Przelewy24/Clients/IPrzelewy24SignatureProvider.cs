using OrchardCore.PaymentGateway.Providers.Przelewy24.Objects;
using System.Threading.Tasks;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Clients
{
    public interface IPrzelewy24SignatureProvider
    {
        Task<string> CreateRegisterSignatureAsync(RegisterRequestDto request, string crcKey);
        Task<string> CreateVerifySignatureAsync(VerifyRequestDto request, string crcKey);
    }
}
