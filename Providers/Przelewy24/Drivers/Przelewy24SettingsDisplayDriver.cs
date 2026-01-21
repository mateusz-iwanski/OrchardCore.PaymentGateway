using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;
using OrchardCore.PaymentGateway.Providers.Przelewy24.Settings;
using OrchardCore.PaymentGateway.Providers.Przelewy24.ViewModels;

namespace OrchardCore.PaymentGateway.Providers.Przelewy24.Drivers;

public class Przelewy24SettingsDisplayDriver : SectionDisplayDriver<ISite, Przelewy24Settings>
{
    public const string GroupId = "Przelewy24";

    public override IDisplayResult Edit(Przelewy24Settings section, BuildEditorContext context)
    {
        if (context.GroupId != GroupId)
        {
            return null!;
        }

        return Initialize<Przelewy24SettingsViewModel>("Przelewy24Settings_Edit", model =>
        {
            model.DefaultAccountKey = section.DefaultAccountKey ?? "default";
            model.Accounts = (section.Accounts ?? new List<Przelewy24AccountSettings>())
                .Select(a => new Przelewy24AccountViewModel
                {
                    Key = a.Key,
                    MerchantId = a.MerchantId,
                    PosId = a.PosId,
                    CrcKey = a.CrcKey,
                    ReportKey = a.ReportKey,
                    SecretId = a.SecretId,
                    BaseUrl = a.BaseUrl,
                    UseSandboxFallbacks = a.UseSandboxFallbacks
                })
                .ToList();
        })
        .Prefix("Przelewy24Settings")  // <-- jawny prefix!
        .Location("Content:5")
        .OnGroup(GroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(Przelewy24Settings section, UpdateEditorContext context)
    {
        if (context.GroupId != GroupId)
        {
            return null!;
        }

        var model = new Przelewy24SettingsViewModel();
        
        // Użyj tego samego prefiksu co w Edit
        await context.Updater.TryUpdateModelAsync(model, "Przelewy24Settings");

        section.DefaultAccountKey = model.DefaultAccountKey?.Trim() ?? "default";

        section.Accounts = (model.Accounts ?? new List<Przelewy24AccountViewModel>())
            .Where(a => !string.IsNullOrWhiteSpace(a.Key))
            .Select(a => new Przelewy24AccountSettings
            {
                Key = a.Key!.Trim(),
                MerchantId = a.MerchantId,
                PosId = a.PosId,
                CrcKey = a.CrcKey?.Trim(),
                ReportKey = a.ReportKey?.Trim(),
                SecretId = a.SecretId?.Trim(),
                BaseUrl = a.BaseUrl?.Trim(),
                UseSandboxFallbacks = a.UseSandboxFallbacks
            })
            .ToList();

        return Edit(section, context);
    }
}
