using SignumSample.Library;

// The sole purpose of this executable is to (a) load the library and (b) report the
// signature state of both binaries, so a single run tells you whether the GitHub
// Actions workflow signed the artifacts.

Console.WriteLine(Greeter.Greet(Environment.UserName));
Console.WriteLine();

var executablePath = Environment.ProcessPath
    ?? throw new InvalidOperationException("Could not determine the executable path.");

var targets = new[]
{
    ("Executable", executablePath),
    ("Library", Greeter.LibraryPath),
};

var anyUnsigned = false;

foreach (var (label, path) in targets)
{
    var info = AuthenticodeInspector.Inspect(path);

    Console.WriteLine($"{label}: {info.FilePath}");
    if (info.IsSigned)
    {
        Console.WriteLine("  Signed     : YES");
        Console.WriteLine($"  Subject    : {info.Subject}");
        Console.WriteLine($"  Issuer     : {info.Issuer}");
        Console.WriteLine($"  Thumbprint : {info.Thumbprint}");
        Console.WriteLine($"  Expires    : {info.NotAfter:yyyy-MM-dd}");
    }
    else
    {
        anyUnsigned = true;
        Console.WriteLine("  Signed     : NO");
        Console.WriteLine($"  Reason     : {info.Error}");
    }

    Console.WriteLine();
}

Console.WriteLine("Note: only the embedded certificate is read; the trust chain is not validated here.");
Console.WriteLine("      Real validation is performed by 'signtool verify /pa' in the workflow.");

// Exit code 1 when any signature is missing. This lets the pipeline use the
// executable as a post-signing check instead of letting unsigned builds through.
return anyUnsigned ? 1 : 0;
