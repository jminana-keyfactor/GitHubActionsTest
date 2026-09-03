namespace SignumSample.Library;

/// <summary>
/// Trivial type that exists only so the executable has a real reason to load
/// this library at runtime.
/// </summary>
public static class Greeter
{
    public static string Greet(string name) => $"Hello, {name}! (from SignumSample.Library)";

    /// <summary>On-disk path of the .dll that contains this type.</summary>
    public static string LibraryPath => typeof(Greeter).Assembly.Location;
}
