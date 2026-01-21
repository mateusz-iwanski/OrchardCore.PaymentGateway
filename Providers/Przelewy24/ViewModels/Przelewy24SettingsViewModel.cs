using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.ViewModels;

public class Przelewy24AccountViewModel
{
    [Required]
    [Display(Name = "Account key (unique)")]
    public string? Key { get; set; }

    [Display(Name = "Merchant ID")] public int? MerchantId { get; set; }
    [Display(Name = "POS ID")] public int? PosId { get; set; }
    [Display(Name = "CRC key"), DataType(DataType.Password)] public string? CrcKey { get; set; }
    [Display(Name = "Report key"), DataType(DataType.Password)] public string? ReportKey { get; set; }
    [Display(Name = "Secret ID"), DataType(DataType.Password)] public string? SecretId { get; set; }
    [Display(Name = "API base URL")] public string? BaseUrl { get; set; }
    [Display(Name = "Use sandbox fallbacks")] public bool UseSandboxFallbacks { get; set; } = true;
}

public class Przelewy24SettingsViewModel
{
    [Display(Name = "Default account key")]
    public string? DefaultAccountKey { get; set; } = "default";

    public List<Przelewy24AccountViewModel> Accounts { get; set; } = new();
}
