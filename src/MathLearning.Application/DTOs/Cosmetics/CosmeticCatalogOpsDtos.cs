namespace MathLearning.Application.DTOs.Cosmetics;

public sealed record CosmeticCatalogFragmentRequirement(
    string Key,
    string FragmentLabel,
    int FragmentsRequired);

public sealed record CosmeticCatalogManifest(
    string RevisionKey,
    string Checksum,
    string UpsertSql,
    DateTime ReleaseDateUtc,
    IReadOnlyList<string> RequiredDefaultKeys,
    IReadOnlyList<CosmeticCatalogFragmentRequirement> RequiredFragments);

public sealed record CosmeticCatalogImportResultDto(
    string RevisionKey,
    string Checksum,
    bool Applied,
    bool AlreadyInstalled,
    int RevisionCount,
    DateTime AppliedAtUtc);

public sealed record CosmeticCatalogReadinessDto(
    bool IsReady,
    string Status,
    string Reason,
    string RevisionKey,
    string Checksum,
    string CatalogVersion,
    int InstalledItemCount,
    IReadOnlyList<string> MissingDefaultKeys,
    IReadOnlyList<string> FragmentIssues,
    IReadOnlyList<string> RewardReferenceIssues);
