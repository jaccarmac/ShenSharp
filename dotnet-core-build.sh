dotnet build -c Release Kl.sln
dotnet test -c Release Kl.Tests
dotnet run -c Release -p Kl.Get
dotnet run -c Release -p Kl.Make
