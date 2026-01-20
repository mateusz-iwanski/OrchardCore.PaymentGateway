using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Settings;

public class Przelewy24Settings
{
    public string? ClientId { get; set; }
    public int? MerchantId { get; set; }
    public int? PosId { get; set; }
    public string? CrcKey { get; set; }
    public string? ReportKey { get; set; }
    public string? SecretId { get; set; }
    public string? BaseUrl { get; set; }
    public bool UseSandboxFallbacks { get; set; } = true;
}
