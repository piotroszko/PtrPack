using System;
using Il2Cpp;
using PtrLib.Mod;
using PtrLib.Networking;

namespace PtrPack.Modules.ClientHelper;

internal sealed partial class ClientHelperModule : PtrModule
{
    private void FillServer(ClientApp client)
    {
        FillServerIp(client.LowestFreeIp);
    }

    private void FillServerIp(string ip)
    {
        if (_setIp == null || string.IsNullOrEmpty(ip))
        {
            Context.Log.Warning("Client helper: Fill skipped; no SetIP or no free IP.");
            return;
        }

        _setIp.ipAddress = ip;
        if (_setIp.ipTextField != null)
            _setIp.ipTextField.text = ip;
        Context.Log.Msg($"Client helper: filled server IP {ip}.");
    }

    private void FillRouter(RouterAuditItem item)
    {
        if (_routerConfig == null)
            return;

        if (item.Action == RouterAction.FillSubnet)
        {
            FillRouterSubnet(item);
            return;
        }

        FillRouterRoute(item);
    }

    private void FillRouterSubnet(RouterAuditItem item)
    {
        if (_routerConfig == null || string.IsNullOrEmpty(item.Subnet))
            return;

        string mask = MaskFromCidr(item.Subnet);
        string subnetIp = item.Subnet;
        int slash = subnetIp.IndexOf('/');
        if (slash >= 0)
            subnetIp = subnetIp.Substring(0, slash);

        _routerConfig.subnet = subnetIp;
        _routerConfig.mask = mask;
        _routerConfig.subnetVlan = item.SourceVlan;

        if (_routerConfig.textSubnet != null)
            _routerConfig.textSubnet.text = subnetIp;
        if (_routerConfig.textMask != null)
            _routerConfig.textMask.text = mask;
        if (_routerConfig.textSubnetVlan != null)
            _routerConfig.textSubnetVlan.text = item.SourceVlan.ToString();

        Context.Log.Msg(
            $"Client helper: filled router subnet {item.Subnet}, VLAN {item.SourceVlan}.");
    }

    private void FillRouterRoute(RouterAuditItem item)
    {
        if (_routerConfig == null)
            return;

        ClientApp client = item.Client;
        if (item.Action == RouterAction.FillRoute && item.SourceVlan >= 0)
            _routerConfig.routeSourceVlan = item.SourceVlan;

        _routerConfig.routeTargetVlan = client.Vlan;
        _routerConfig.routeSourceIp = string.Empty;
        _routerConfig.routeTargetIp = string.Empty;
        if (_routerConfig.textRouteSource != null)
            _routerConfig.textRouteSource.text = item.SourceVlan.ToString();
        if (_routerConfig.textRouteTarget != null)
            _routerConfig.textRouteTarget.text = client.Vlan.ToString();

        Context.Log.Msg(
            $"Client helper: filled router route VLAN {item.SourceVlan} -> VLAN {client.Vlan}.");
    }

    private static string MaskFromCidr(string subnet)
    {
        if (subnet.IndexOf('/') < 0)
            return subnet;

        if (!Ipv4Subnet.TryParseCidr(subnet, out _, out int prefix))
            return "255.255.255.0";

        return Ipv4Address.Format(Ipv4Subnet.Mask(prefix));
    }
}
