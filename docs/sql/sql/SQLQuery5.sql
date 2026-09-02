USE [TSU-Dashboard]
GO

/****** Object:  Table [dbo].[ProjectWorkers]    Script Date: 9/1/2026 6:36:28 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ProjectWorkers](
	[ProjectWorkerId] [int] IDENTITY(1,1) NOT NULL,
	[DisplayName] [nvarchar](100) NOT NULL,
	[IsActive] [bit] NOT NULL,
	[SortOrder] [int] NOT NULL,
	[CreatedUtc] [datetime2](0) NOT NULL,
	[UpdatedUtc] [datetime2](0) NOT NULL,
 CONSTRAINT [PK_ProjectWorkers] PRIMARY KEY CLUSTERED 
(
	[ProjectWorkerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ProjectWorkers] ADD  CONSTRAINT [DF_ProjectWorkers_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO

ALTER TABLE [dbo].[ProjectWorkers] ADD  CONSTRAINT [DF_ProjectWorkers_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO

ALTER TABLE [dbo].[ProjectWorkers] ADD  CONSTRAINT [DF_ProjectWorkers_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO

ALTER TABLE [dbo].[ProjectWorkers] ADD  CONSTRAINT [DF_ProjectWorkers_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO


