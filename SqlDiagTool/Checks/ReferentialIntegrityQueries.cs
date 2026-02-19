namespace SqlDiagTool.Checks;

/// hared SQL for checks that find parent-child column pairs with no FK constraint
internal static class ReferentialIntegrityQueries
{
    public const string ParentChildWithoutFk = """
        WITH pk_cols AS (
            SELECT s.name AS ps, t.name AS pt, c.name AS pc, t.object_id AS p_obj, c.column_id AS p_cid
            FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id AND i.type = 1
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            JOIN sys.tables t ON t.object_id = ic.object_id
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
        ),
        child_cols AS (
            SELECT s.name AS cs, t.name AS ct, c.name AS cc, t.object_id AS c_obj, c.column_id AS c_cid
            FROM sys.columns c
            JOIN sys.tables t ON t.object_id = c.object_id
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
        )
        SELECT child.cs, child.ct, child.cc, pk.ps, pk.pt
        FROM pk_cols pk
        JOIN child_cols child ON pk.pc = child.cc AND (pk.ps <> child.cs OR pk.pt <> child.ct)
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            WHERE fkc.parent_object_id = child.c_obj AND fkc.referenced_object_id = pk.p_obj
              AND fkc.parent_column_id = child.c_cid AND fkc.referenced_column_id = pk.p_cid
        )
        ORDER BY child.cs, child.ct, pk.ps, pk.pt
        """;

    // FKs whose referenced column is not in any UNIQUE or PK index
    public const string FkTargetNotUnique = """
        WITH unique_key_cols AS (
            SELECT ic.object_id, ic.column_id
            FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            WHERE (i.type = 1 OR i.is_unique = 1)
        ),
        fk_refs AS (
            SELECT
                OBJECT_SCHEMA_NAME(fk.parent_object_id) AS parent_schema,
                OBJECT_NAME(fk.parent_object_id) AS parent_table,
                fk.name AS fk_name,
                OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ref_schema,
                OBJECT_NAME(fk.referenced_object_id) AS ref_table,
                c.name AS ref_column,
                fk.referenced_object_id AS ref_obj_id,
                fkc.referenced_column_id AS ref_col_id
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns c ON c.object_id = fkc.referenced_object_id AND c.column_id = fkc.referenced_column_id
            JOIN sys.tables pt ON pt.object_id = fk.parent_object_id AND pt.is_ms_shipped = 0
            JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id AND rt.is_ms_shipped = 0
        )
        SELECT parent_schema, parent_table, fk_name, ref_schema, ref_table, ref_column
        FROM fk_refs
        WHERE NOT EXISTS (
            SELECT 1 FROM unique_key_cols uk
            WHERE uk.object_id = fk_refs.ref_obj_id AND uk.column_id = fk_refs.ref_col_id
        )
        ORDER BY parent_schema, parent_table, fk_name
        """;

    // FK columns on parent side that allow NULL
    public const string NullableFkColumns = """
        SELECT OBJECT_SCHEMA_NAME(fk.parent_object_id), OBJECT_NAME(fk.parent_object_id), fk.name, c.name
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
        JOIN sys.tables t ON t.object_id = fk.parent_object_id AND t.is_ms_shipped = 0
        WHERE c.is_nullable = 1
        ORDER BY 1, 2, 3
        """;

    // Delete/update action per FK for review
    public const string FkCascadeRules = """
        SELECT OBJECT_SCHEMA_NAME(fk.parent_object_id), OBJECT_NAME(fk.parent_object_id), fk.name,
            fk.delete_referential_action_desc, fk.update_referential_action_desc
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON t.object_id = fk.parent_object_id AND t.is_ms_shipped = 0
        ORDER BY 1, 2, 3
        """;

    // Edges for FK graph: parent_object_id, referenced_object_id, and names for cycle reporting
    public const string FkEdges = """
        SELECT fk.parent_object_id, fk.referenced_object_id,
            OBJECT_SCHEMA_NAME(fk.parent_object_id), OBJECT_NAME(fk.parent_object_id),
            OBJECT_SCHEMA_NAME(fk.referenced_object_id), OBJECT_NAME(fk.referenced_object_id)
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON t.object_id = fk.parent_object_id AND t.is_ms_shipped = 0
        """;

    // Single-column FK where referenced table has single-column PK but parent FK column has no UNIQUE (possible 1:1)
    public const string OneToOneMissingUnique = """
        WITH one_col_fk AS (
            SELECT fk.parent_object_id, fk.referenced_object_id, MAX(fkc.parent_column_id) AS parent_column_id
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.tables pt ON pt.object_id = fk.parent_object_id AND pt.is_ms_shipped = 0
            JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id AND rt.is_ms_shipped = 0
            GROUP BY fk.object_id, fk.parent_object_id, fk.referenced_object_id
            HAVING COUNT(*) = 1
        ),
        ref_single_pk AS (
            SELECT i.object_id
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            WHERE i.type = 1
            GROUP BY i.object_id
            HAVING COUNT(*) = 1
        ),
        parent_in_unique AS (
            SELECT ic.object_id, ic.column_id
            FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            WHERE i.type = 1 OR i.is_unique = 1
        )
        SELECT OBJECT_SCHEMA_NAME(f.parent_object_id), OBJECT_NAME(f.parent_object_id), c.name
        FROM one_col_fk f
        JOIN ref_single_pk r ON r.object_id = f.referenced_object_id
        JOIN sys.columns c ON c.object_id = f.parent_object_id AND c.column_id = f.parent_column_id
        WHERE NOT EXISTS (
            SELECT 1 FROM parent_in_unique u
            WHERE u.object_id = f.parent_object_id AND u.column_id = f.parent_column_id
        )
        ORDER BY 1, 2, 3
        """;

    // Pairs of columns like owner_type + owner_id in same table where _id has no FK (polymorphic pattern)
    public const string PolymorphicTypeIdPairs = """
        SELECT OBJECT_SCHEMA_NAME(t.object_id), OBJECT_NAME(t.object_id), c_type.name, c_id.name
        FROM sys.columns c_type
        JOIN sys.columns c_id ON c_id.object_id = c_type.object_id
            AND c_id.column_id <> c_type.column_id
            AND c_type.name LIKE '%_type' AND c_id.name LIKE '%_id'
            AND LEFT(c_type.name, LEN(c_type.name) - 5) = LEFT(c_id.name, LEN(c_id.name) - 3)
        JOIN sys.tables t ON t.object_id = c_type.object_id AND t.is_ms_shipped = 0
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.foreign_key_columns fkc
            WHERE fkc.parent_object_id = c_id.object_id AND fkc.parent_column_id = c_id.column_id
        )
        ORDER BY 1, 2, 3
        """;
}
