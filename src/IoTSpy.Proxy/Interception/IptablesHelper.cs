using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Proxy.Interception;

/// <summary>
/// Manages iptables REDIRECT rules for GatewayRedirect mode.
/// Requires root/CAP_NET_ADMIN to execute iptables commands.
/// </summary>
public sealed class IptablesHelper(ILogger<IptablesHelper> logger)
{
    private readonly List<string> _activeRules = [];

    /// <summary>Default ports to redirect through the transparent proxy.</summary>
    public static readonly int[] DefaultRedirectPorts = [80, 443, 1883, 8883];

    /// <summary>
    /// Installs iptables REDIRECT rules for the given destination ports.
    /// Traffic to these ports on the PREROUTING chain will be redirected to the transparent proxy port.
    /// </summary>
    public async Task<bool> InstallRedirectRulesAsync(int transparentProxyPort, int[]? ports = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            logger.LogWarning("iptables rules only supported on Linux");
            return false;
        }

        ports ??= DefaultRedirectPorts;

        // Enable IP forwarding
        if (!await RunCommandAsync("sysctl", "-w net.ipv4.ip_forward=1"))
            logger.LogWarning("Failed to enable IP forwarding — GatewayRedirect may not work");

        foreach (var port in ports)
        {
            var args = $"-t nat -A PREROUTING -p tcp --dport {port} -j REDIRECT --to-port {transparentProxyPort}";
            if (await RunCommandAsync("iptables", args))
            {
                _activeRules.Add(args);
                logger.LogInformation("iptables rule installed: {Args}", args);
            }
            else
            {
                logger.LogError("Failed to install iptables rule: {Args}", args);
            }
        }

        return _activeRules.Count > 0;
    }

    /// <summary>
    /// Removes all iptables REDIRECT rules that were installed by this helper.
    /// </summary>
    public async Task RemoveRedirectRulesAsync()
    {
        foreach (var rule in _activeRules)
        {
            // Replace -A (append) with -D (delete) to remove the rule
            var deleteArgs = rule.Replace("-A PREROUTING", "-D PREROUTING");
            if (await RunCommandAsync("iptables", deleteArgs))
                logger.LogInformation("iptables rule removed: {Args}", deleteArgs);
            else
                logger.LogWarning("Failed to remove iptables rule: {Args}", deleteArgs);
        }
        _activeRules.Clear();
    }

    private async Task<bool> RunCommandAsync(string command, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                logger.LogDebug("{Command} {Args} failed: {Error}", command, arguments, stderr);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to run {Command} {Args}", command, arguments);
            return false;
        }
    }
}
