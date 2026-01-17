using System.Collections.Generic;

namespace ApiClientPrzelewy24.Objects
{
    /// <summary>
    /// Lista aliasów BLIK dla adresu email
    /// </summary>
    public record BlikAliasesResponseDto
    {
        public BlikAliasesData? Data { get; init; }
    }

    public record BlikAliasesData
    {
        public List<BlikAlias>? Aliases { get; init; }
    }

    public record BlikAlias
    {
        public string? AliasValue { get; init; }
        public string? AliasLabel { get; init; }
        public string? Type { get; init; }
    }
}