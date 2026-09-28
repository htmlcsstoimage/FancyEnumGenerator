using BenchmarkDotNet.Running;

// Quick run (~6 min):   dotnet run -c Release -- --filter "*" --job short
// Full precision:       dotnet run -c Release -- --filter "*"
// One class:            dotnet run -c Release -- --filter "*ParseBenchmarks*" --job short
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
