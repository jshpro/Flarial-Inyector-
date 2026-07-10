namespace Flarial.Runtime.Client;

public sealed class FlarialClientBeta : FlarialClient<FlarialClientBeta>
{
    private protected override string Build { get; } = "Beta";
    private protected override string FileName { get; } = "Flarial.Client.Beta.dll";
    private protected override string DownloadUri { get; } = "https://cdn.flarial.xyz/dll/beta.dll";
}