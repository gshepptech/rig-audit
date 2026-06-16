using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using RigAudit.Core.Models;

namespace RigAudit.Collectors.Collectors;

public class DeviceInventoryCollector : ICollector
{
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfAllClasses = 0x00000004;
    private const uint SpdrpDeviceDesc = 0x00000000;
    private const uint SpdrpClass = 0x00000007;
    private const uint SpdrpMfg = 0x0000000B;
    private const uint SpdrpFriendlyName = 0x0000000C;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public string Name => "DeviceInventory";

    public void Collect(RigSnapshot snapshot)
    {
        snapshot.Devices = [];

        var driverDetails = LoadDriverDetailsByName();
        var deviceSet = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DigcfPresent | DigcfAllClasses);
        if (deviceSet == IntPtr.Zero || deviceSet == InvalidHandleValue)
            return;

        try
        {
            var index = 0u;
            var deviceInfoData = new SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SpDevinfoData>() };
            while (SetupDiEnumDeviceInfo(deviceSet, index, ref deviceInfoData))
            {
                index++;

                var className = GetStringProperty(deviceSet, ref deviceInfoData, SpdrpClass);
                var friendlyName = GetStringProperty(deviceSet, ref deviceInfoData, SpdrpFriendlyName)
                                   ?? GetStringProperty(deviceSet, ref deviceInfoData, SpdrpDeviceDesc);

                if (string.IsNullOrWhiteSpace(friendlyName))
                    continue;

                var category = MapCategory(className, friendlyName);
                if (category is null)
                    continue;

                var wmiInfo = driverDetails.TryGetValue(friendlyName, out var details) ? details : null;
                snapshot.Devices.Add(new DeviceInfo
                {
                    Category = category,
                    FriendlyName = friendlyName,
                    DriverProvider = wmiInfo?.Provider ?? GetStringProperty(deviceSet, ref deviceInfoData, SpdrpMfg),
                    DriverVersion = wmiInfo?.Version,
                    DriverDate = wmiInfo?.Date
                });
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(deviceSet);
        }
    }

    private static Dictionary<string, DriverDetails> LoadDriverDetailsByName()
    {
        var map = new Dictionary<string, DriverDetails>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceName, DriverProviderName, DriverVersion, DriverDate FROM Win32_PnPSignedDriver");
            foreach (var item in searcher.Get())
            {
                var name = item["DeviceName"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                map[name] = new DriverDetails
                {
                    Provider = item["DriverProviderName"]?.ToString(),
                    Version = item["DriverVersion"]?.ToString(),
                    Date = item["DriverDate"]?.ToString()
                };
            }
        }
        catch
        {
            // Best effort only.
        }

        return map;
    }

    private static string? MapCategory(string? className, string deviceName)
    {
        if (string.Equals(className, "Net", StringComparison.OrdinalIgnoreCase))
            return "Network Adapter";
        if (string.Equals(className, "MEDIA", StringComparison.OrdinalIgnoreCase))
            return "Audio Device";
        if (string.Equals(className, "HIDClass", StringComparison.OrdinalIgnoreCase))
            return "HID Device";

        if (deviceName.Contains("controller", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("gamepad", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Contains("joystick", StringComparison.OrdinalIgnoreCase))
        {
            return "Game Controller";
        }

        return null;
    }

    private static string? GetStringProperty(IntPtr deviceSet, ref SpDevinfoData infoData, uint property)
    {
        var buffer = new byte[1024];
        if (!SetupDiGetDeviceRegistryProperty(
                deviceSet,
                ref infoData,
                property,
                out _,
                buffer,
                (uint)buffer.Length,
                out var requiredSize))
        {
            return null;
        }

        if (requiredSize == 0)
            return null;

        var text = Encoding.Unicode.GetString(buffer, 0, (int)requiredSize);
        return text.TrimEnd('\0').Trim();
    }

    private sealed class DriverDetails
    {
        public string? Provider { get; set; }
        public string? Version { get; set; }
        public string? Date { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevinfoData
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        IntPtr classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(
        IntPtr deviceInfoSet,
        uint memberIndex,
        ref SpDevinfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr deviceInfoSet,
        ref SpDevinfoData deviceInfoData,
        uint property,
        out uint propertyRegDataType,
        byte[] propertyBuffer,
        uint propertyBufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
}
