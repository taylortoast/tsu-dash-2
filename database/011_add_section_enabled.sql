USE [TSU-Dashboard];
GO

/* Adds the global section availability flag. Safe to rerun. */
IF COL_LENGTH(N'dbo.Sections', N'IsEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Sections
    ADD IsEnabled BIT NOT NULL
        CONSTRAINT DF_Sections_IsEnabled DEFAULT (1)
        WITH VALUES;
END
GO

SELECT SectionId, SectionCode, SectionName, SortOrder, IsPublicVisible, IsEnabled
FROM dbo.Sections
ORDER BY SortOrder;
GO
