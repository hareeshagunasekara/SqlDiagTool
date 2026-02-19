using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SqlDiagTool.Checks;

// Default set of structure checks for the runner; add new checks here without changing runner logic.
public static class CheckRegistry
{
    private static readonly Lazy<IReadOnlyList<IStructureCheck>> DefaultChecks =
        new(() => CreateAll(NullLoggerFactory.Instance));

    private static readonly Lazy<IReadOnlyList<string>> DefaultCategories =
        new(() => DefaultChecks.Value
            .Select(c => c.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList());

    public static IReadOnlyList<string> Categories => DefaultCategories.Value;

    public static IReadOnlyList<IStructureCheck> All => DefaultChecks.Value;

    public static IReadOnlyList<IStructureCheck> CreateAll(ILoggerFactory loggerFactory)
    {
        ILogger Log<T>() => loggerFactory.CreateLogger(typeof(T).Name);

        return new IStructureCheck[]
        {
            new MissingPrimaryKeysCheck(),
            new HeapTablesCheck(),
            new ExtremeNullableRatioCheck(),
            new SuspectedJunctionMissingKeyCheck(),
            new MissingCheckConstraintsCheck(),
            new MissingUniqueConstraintsCheck(),
            new MissingForeignKeysCheck(),
            new OrphanRecordsCheck(Log<OrphanRecordsCheck>()),
            new ForeignKeyTypeMismatchCheck(),
            new MoneyStoredAsFloatCheck(),
            new MissingIndexSuggestionsCheck(),
            new UnusedIndexesCheck(),
            new FragmentationCheck(),
            new SchemaSummaryCheck(),
            new DuplicateRecordsCheck(Log<DuplicateRecordsCheck>()),
            new InconsistentFormatsCheck(Log<InconsistentFormatsCheck>()),
            new BrokenBusinessRulesCheck(Log<BrokenBusinessRulesCheck>()),
            new StatusTypeConstraintConsistencyCheck(),
            new NullablePrimaryKeyCheck(Log<NullablePrimaryKeyCheck>()),
            new CompositePrimaryKeyCheck(),
            new DuplicateIdentityColumnsCheck(),
            new NaturalSurrogateKeyHeuristicCheck(),
            new ForeignKeyTargetNotUniqueCheck(),
            new NullableForeignKeyColumnsCheck(),
            new ForeignKeyCascadeRulesCheck(),
            new CircularForeignKeyCheck(),
            new OneToOneMissingUniqueCheck(),
            new PolymorphicRelationshipCheck()
        };
    }
}
