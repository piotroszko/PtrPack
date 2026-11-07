using System.Collections.Generic;
using UnityEngine;

namespace PtrPack.Modules.ClientHelper;

internal sealed class ClientApp
{
    internal ClientApp(
        int customerBaseId,
        int customerId,
        string customerName,
        int appId,
        string subnet,
        int vlan,
        string gateway,
        string lowestFreeIp,
        int usedIps,
        int totalIps,
        int requiredServerType,
        Sprite? logo)
    {
        CustomerBaseId = customerBaseId;
        CustomerId = customerId;
        CustomerName = customerName;
        AppId = appId;
        Subnet = subnet;
        Vlan = vlan;
        Gateway = gateway;
        LowestFreeIp = lowestFreeIp;
        UsedIps = usedIps;
        TotalIps = totalIps;
        RequiredServerType = requiredServerType;
        Logo = logo;
    }

    internal int CustomerBaseId { get; }
    internal int CustomerId { get; }
    internal string CustomerName { get; }
    internal int AppId { get; }
    internal string Subnet { get; }
    internal int Vlan { get; }
    internal string Gateway { get; }
    internal string LowestFreeIp { get; }
    internal int UsedIps { get; }
    internal int TotalIps { get; }
    internal int RequiredServerType { get; }
    internal Sprite? Logo { get; }

    internal List<RoutedIpOption> RoutedOptions { get; } = new List<RoutedIpOption>();

    internal bool HasFreeIp => !string.IsNullOrEmpty(LowestFreeIp);

    internal string Title => $"{CustomerName}  •  App {AppId}";
}
