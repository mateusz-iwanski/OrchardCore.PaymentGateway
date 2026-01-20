using System.Collections.Generic;
using System.Threading.Tasks;
using OrchardCore.Security.Permissions;

namespace OrchardCore.PaymentGateway;

public class Permissions : IPermissionProvider
{
    public static readonly Permission ManagePaymentGateway =
        new Permission(nameof(ManagePaymentGateway), "Manage Przelewy24 payment gateway settings");

    public Task<IEnumerable<Permission>> GetPermissionsAsync() =>
        Task.FromResult<IEnumerable<Permission>>(new[] { ManagePaymentGateway });

    public IEnumerable<Permission> GetPermissions() =>
        new[] { ManagePaymentGateway };

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
        new[]
        {
            new PermissionStereotype
            {
                Name = "Administrator",
                Permissions = new[] { ManagePaymentGateway }
            },
            new PermissionStereotype
            {
                Name = "SiteOwner",
                Permissions = new[] { ManagePaymentGateway }
            }
        };
}