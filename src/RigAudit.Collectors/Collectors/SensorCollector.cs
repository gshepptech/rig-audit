using LibreHardwareMonitor.Hardware;
using RigAudit.Core.Models;

namespace RigAudit.Collectors.Collectors;

public class SensorCollector : ICollector
{
    public string Name => "Sensors";

    public void Collect(RigSnapshot snapshot)
    {
        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true
        };

        try
        {
            computer.Open();

            foreach (var hardware in computer.Hardware)
            {
                hardware.Update();

                if (hardware.HardwareType == HardwareType.Cpu)
                    CollectCpuSensors(hardware, snapshot);

                if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                    CollectGpuSensors(hardware, snapshot);
            }
        }
        finally
        {
            computer.Close();
        }
    }

    private static void CollectCpuSensors(IHardware hardware, RigSnapshot snapshot)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature &&
                sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) &&
                sensor.Value.HasValue)
            {
                snapshot.Sensors.CpuPackageTempC = Math.Round(sensor.Value.Value, 1);
            }

            if (sensor.SensorType == SensorType.Load &&
                sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase) &&
                sensor.Value.HasValue)
            {
                snapshot.Sensors.CpuLoadPercent = Math.Round(sensor.Value.Value, 1);
            }
        }
    }

    private static void CollectGpuSensors(IHardware hardware, RigSnapshot snapshot)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature &&
                sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) &&
                sensor.Value.HasValue &&
                snapshot.Sensors.GpuTempC is null)
            {
                snapshot.Sensors.GpuTempC = Math.Round(sensor.Value.Value, 1);
            }

            if (sensor.SensorType == SensorType.Load &&
                sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) &&
                sensor.Value.HasValue &&
                snapshot.Sensors.GpuLoadPercent is null)
            {
                snapshot.Sensors.GpuLoadPercent = Math.Round(sensor.Value.Value, 1);
            }
        }
    }
}
