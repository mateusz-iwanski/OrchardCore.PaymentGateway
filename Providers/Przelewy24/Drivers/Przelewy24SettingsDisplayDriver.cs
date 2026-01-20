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
            model.ClientId = section.ClientId;
            model.MerchantId = section.MerchantId;
            model.PosId = section.PosId;
            model.CrcKey = section.CrcKey;
            model.ReportKey = section.ReportKey;
            model.SecretId = section.SecretId;
            model.BaseUrl = section.BaseUrl;
            model.UseSandboxFallbacks = section.UseSandboxFallbacks;
        })
        .Location("Content:5")
        .OnGroup(GroupId);
    }

    public override async Task<IDisplayResult> UpdateAsync(Przelewy24Settings section, UpdateEditorContext context)
    {
        if (context.GroupId != GroupId)
        {
            // Fix: Use the non-obsolete overload with ISite as required
            return await base.UpdateAsync(context.Updater is ISite site ? site : default!, section, context);
        }

        var model = new Przelewy24SettingsViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        section.ClientId = model.ClientId?.Trim();
        section.MerchantId = model.MerchantId;
        section.PosId = model.PosId;
        section.CrcKey = model.CrcKey?.Trim();
        section.ReportKey = model.ReportKey?.Trim();
        section.SecretId = model.SecretId?.Trim();
        section.BaseUrl = model.BaseUrl?.Trim();
        section.UseSandboxFallbacks = model.UseSandboxFallbacks;

        // Fix: Pass the correct type to Edit
        return Edit(section, context);
    }
}
