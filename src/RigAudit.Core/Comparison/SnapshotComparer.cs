using RigAudit.Core.Models;

namespace RigAudit.Core.Comparison;

public static class SnapshotComparer
{
    public static SnapshotCompareResult Compare(RigSnapshot first, RigSnapshot second)
    {
        var result = new SnapshotCompareResult();

        if (!string.Equals(first.GPU.DriverVersion, second.GPU.DriverVersion, StringComparison.OrdinalIgnoreCase))
        {
            result.Changes.Add(
                $"GPU driver version changed: {Display(first.GPU.DriverVersion)} -> {Display(second.GPU.DriverVersion)}");
        }

        if (!string.Equals(first.OS.WindowsBuildNumber, second.OS.WindowsBuildNumber, StringComparison.OrdinalIgnoreCase))
        {
            result.Changes.Add(
                $"Windows build changed: {Display(first.OS.WindowsBuildNumber)} -> {Display(second.OS.WindowsBuildNumber)}");
        }

        if (!string.Equals(first.Power.ActivePowerPlanName, second.Power.ActivePowerPlanName, StringComparison.OrdinalIgnoreCase))
        {
            result.Changes.Add(
                $"Power plan changed: {Display(first.Power.ActivePowerPlanName)} -> {Display(second.Power.ActivePowerPlanName)}");
        }

        if (first.Memory.ConfiguredClockMhz != second.Memory.ConfiguredClockMhz)
        {
            result.Changes.Add(
                $"Memory speed changed: {Display(first.Memory.ConfiguredClockMhz?.ToString())} -> {Display(second.Memory.ConfiguredClockMhz?.ToString())}");
        }

        var firstSet = BuildDeviceSet(first.Devices);
        var secondSet = BuildDeviceSet(second.Devices);

        var added = secondSet.Except(firstSet, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var removed = firstSet.Except(secondSet, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

        foreach (var item in added)
            result.Changes.Add($"Device added: {item}");
        foreach (var item in removed)
            result.Changes.Add($"Device removed: {item}");

        if (result.Changes.Count == 0)
            result.Changes.Add("No tracked changes detected.");

        return result;
    }

    private static HashSet<string> BuildDeviceSet(IEnumerable<DeviceInfo>? devices)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (devices is null)
            return set;

        foreach (var device in devices)
        {
            var key = $"{Display(device.Category)} | {Display(device.FriendlyName)} | {Display(device.DriverVersion)}";
            set.Add(key);
        }

        return set;
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
}
