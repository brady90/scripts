using System;
using System.IO;
using System.Management;
using System.Runtime.Versioning;

class DefenderMonitor
{
    private const string LogPath = @"C:\Users\mcafe\Documnets\defender_status.log";
    private static bool? _lastState = null;


    [SupportedOSPlatform("Windows")]
    static void Main()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        Log("Monitor started (event-driven).");

        // Log the initial state once at startup
        _lastState = GetRealTimeProtectionStatus();
        if (_lastState.HasValue)
            Log($"Initial state: {(_lastState.Value ? "ON" : "OFF")}");

        var scope = new ManagementScope(@"root\Microsoft\Windows\Defender");
        scope.Connect();

        // WITHIN 5 tells WMI to check for changes every 5 seconds
        // and only fire the event when RealTimeProtectionEnabled actually changes.
        string query =
            "SELECT * FROM __InstanceModificationEvent WITHIN 5 " +
            "WHERE TargetInstance ISA 'MSFT_MpComputerStatus'";

        using var watcher = new ManagementEventWatcher(scope, new WqlEventQuery(query));
        watcher.EventArrived += Watcher_EventArrived;
        watcher.Start();

        Log("Watching for changes. Press Enter to exit.");
        Console.ReadLine();

        watcher.Stop();
    }
    [SupportedOSPlatform("Windows")]
    static void Watcher_EventArrived(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var targetInstance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            bool enabled = (bool)targetInstance["RealTimeProtectionEnabled"];

            if (enabled != _lastState)
            {
                string state = enabled ? "ON" : "OFF";
                Log($"Real-time protection turned {state}");
                _lastState = enabled;
            }
        }
        catch (Exception ex)
        {
            Log($"Error processing event: {ex.Message}");
        }
    }
    [SupportedOSPlatform("Windows")]
    static bool? GetRealTimeProtectionStatus()
    {
        var scope = new ManagementScope(@"root\Microsoft\Windows\Defender");
        var query = new ObjectQuery("SELECT RealTimeProtectionEnabled FROM MSFT_MpComputerStatus");

        using var searcher = new ManagementObjectSearcher(scope, query);
        foreach (ManagementObject obj in searcher.Get())
        {
            return (bool)obj["RealTimeProtectionEnabled"];
        }

        return null;
    }

    static void Log(string message)
    {
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
        Console.WriteLine(line);
        File.AppendAllText(LogPath, line + Environment.NewLine);
    }
}