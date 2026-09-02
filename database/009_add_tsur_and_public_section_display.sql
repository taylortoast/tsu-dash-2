USE [TSU-Dashboard];
GO

/*
Adds TSUR and the per-section public-board display flag.
Safe to rerun.
*/

IF COL_LENGTH(N'dbo.Sections', N'IsPublicVisible') IS NULL
BEGIN
    ALTER TABLE dbo.Sections
    ADD IsPublicVisible BIT NOT NULL
        CONSTRAINT DF_Sections_IsPublicVisible DEFAULT 1
        WITH VALUES;
END;
GO

MERGE dbo.Sections AS target
USING (VALUES
    (N'TSU',  N'TSU Flight', 1, 1),
    (N'TSUI', N'TSUI', 2, 1),
    (N'TSUL', N'TSUL', 3, 1),
    (N'TSUS', N'TSUS', 4, 1),
    (N'TSUR', N'TSUR', 5, 1)
) AS source (SectionCode, SectionName, SortOrder, IsPublicVisible)
ON target.SectionCode = source.SectionCode
WHEN MATCHED THEN
    UPDATE SET
        SectionName = source.SectionName,
        SortOrder = source.SortOrder
WHEN NOT MATCHED BY TARGET THEN
    INSERT (SectionCode, SectionName, SortOrder, IsPublicVisible)
    VALUES (source.SectionCode, source.SectionName, source.SortOrder, source.IsPublicVisible);
GO

SELECT
    SectionId,
    SectionCode,
    SectionName,
    SortOrder,
    IsPublicVisible
FROM dbo.Sections
ORDER BY SortOrder;
GO
