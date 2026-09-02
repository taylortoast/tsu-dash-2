USE [TSU-Dashboard];
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
