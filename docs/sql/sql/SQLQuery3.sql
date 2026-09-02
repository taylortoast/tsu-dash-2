USE [TSU-Dashboard]
GO

/****** Object:  Table [dbo].[ProjectBoardState]    Script Date: 9/1/2026 6:36:14 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ProjectBoardState](
	[PostId] [int] NOT NULL,
	[Category] [nvarchar](50) NOT NULL,
	[UpdatedByUserId] [int] NOT NULL,
	[UpdatedUtc] [datetime2](0) NOT NULL,
 CONSTRAINT [PK_ProjectBoardState] PRIMARY KEY CLUSTERED 
(
	[PostId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[ProjectBoardState] ADD  CONSTRAINT [DF_ProjectBoardState_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO

ALTER TABLE [dbo].[ProjectBoardState]  WITH CHECK ADD  CONSTRAINT [FK_ProjectBoardState_Posts] FOREIGN KEY([PostId])
REFERENCES [dbo].[Posts] ([PostId])
GO

ALTER TABLE [dbo].[ProjectBoardState] CHECK CONSTRAINT [FK_ProjectBoardState_Posts]
GO

ALTER TABLE [dbo].[ProjectBoardState]  WITH CHECK ADD  CONSTRAINT [FK_ProjectBoardState_UpdatedBy] FOREIGN KEY([UpdatedByUserId])
REFERENCES [dbo].[Users] ([UserId])
GO

ALTER TABLE [dbo].[ProjectBoardState] CHECK CONSTRAINT [FK_ProjectBoardState_UpdatedBy]
GO


