using SignumSample.Library;

// The sole purpose of this executable is to (a) load the library and (b) report the
// signature state of both binaries, so a single run tells you whether the GitHub
// Actions workflow signed the artifacts.
//
// By default it only reports and exits 0, so it is usable as a smoke test on an
// unsigned build. Pass --require-signatures to make a missing signature a failure,
// which is how the signing job gates its own output.

var requireSignatures = args.Contains("--require-signatures", StringComparer.OrdinalIgnoreCase);

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

if (anyUnsigned && requireSignatures)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("At least one binary is unsigned and --require-signatures was given.");
    return 1;
}

return 0;
