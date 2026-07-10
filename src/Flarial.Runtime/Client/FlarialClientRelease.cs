namespace Flarial.Runtime.Client;

public sealed class FlarialClientRelease : FlarialClient<FlarialClientRelease>
{
    private protected override string Build { get; } = "Release";
    private protected override string FileName { get; } = "Flarial.Client.Release.dll";
    private protected override string DownloadUri { get; } = "https://cdn.flarial.xyz/dll/latest.dll";
}