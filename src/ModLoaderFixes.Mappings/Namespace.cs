namespace ModLoaderFixes.Mappings;

/// <summary>The kinds of names that mappings give a version's obfuscated classes and methods.</summary>
internal enum Namespace
{
    /// <summary>MCP's, which Forge runs under up to Minecraft 1.16.5, from Forge's SRG and TSRG files.</summary>
    Srg,

    /// <summary>Ornithe's intermediary names, which stay the same from one version to the next, from Calamus up to Minecraft 1.14.4.</summary>
    Intermediary,

    /// <summary>Mojang's, which Forge from 1.17 and NeoForge run under, from Mojang's mappings from 1.14.4 and from the unobfuscated jars from 26.1.</summary>
    Official,
}
