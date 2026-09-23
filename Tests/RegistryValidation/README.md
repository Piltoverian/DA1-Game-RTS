# Registry validation checks

Run from repository root with .NET 10 SDK:

```powershell
dotnet run --project Tests/RegistryValidation/RegistryValidation.csproj
```

This dependency-free console suite compiles the actual definition and validation sources via linked files. Test-only UnityEngine/ResourcePair adapters permit exercising membership, identity relationships, diagnostics, cycles, costs and read-only behavior outside Unity. All files are outside Assets and do not enter gameplay assemblies. No game assets are created or modified.

This is not Unity EditMode/PlayMode verification. Native Unity object lifetime/null semantics, OnValidate, serialization, Inspector, baking and rebake require separate checks in Unity. Runtime and Editor source compilation with real Unity references is also checked separately.
