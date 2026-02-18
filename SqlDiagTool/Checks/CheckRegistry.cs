namespace SqlDiagTool.Checks;

// Default set of structure checks for the runner; add new checks here without changing runner logic.
public static class CheckRegistry
{
    public static IReadOnlyList<IStructureCheck> All => new IStructureCheck[]
    {
        new MissingPrimaryKeysCheck(),
        new HeapTablesCheck(),
        new ExtremeNullableRatioCheck(),
        new SuspectedJunctionMissingKeyCheck(),
        new MissingCheckConstraintsCheck(),
        new MissingUniqueConstraintsCheck(),
        new MissingForeignKeysCheck(),
        new OrphanRecordsCheck(),
        new ForeignKeyTypeMismatchCheck(),
        new MoneyStoredAsFloatCheck(),
        new MissingIndexSuggestionsCheck(),
        new UnusedIndexesCheck(),
        new FragmentationCheck(),
        new TopSlowQueriesCheck()
    };
}
