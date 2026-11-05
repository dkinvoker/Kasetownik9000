namespace Kasetownik9000;

/// <summary>Informacje o wersji (numer pochodzi z src/Directory.Build.props).</summary>
public static class AppInfo
{
    private static readonly Version AssemblyVersion = typeof(AppInfo).Assembly.GetName().Version ?? new Version(1, 0);

    /// <summary>Krótki numer, np. "1.5" (z trzecią cyfrą tylko gdy nie jest zerem: "1.5.1").</summary>
    public static string Version => AssemblyVersion.Build > 0
        ? $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}"
        : $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}";

    public static string Name => "Kasetownik 9000";

    public static string FullName => $"{Name} v{Version}";
}
