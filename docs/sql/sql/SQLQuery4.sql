USE [TSU-Dashboard]
GO

/****** Object:  Table [dbo].[ProjectNotes]    Script Date: 9/1/2026 6:36:22 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ProjectNotes](
	[ProjectNoteId] [int] IDENTITY(1,1) NOT NULL,
	[PostId] [int] NOT NULL,
	[NoteText] [nvarchar](max) NOT NULL,
	[CreatedByUserId] [int] NOT NULL,
	[CreatedUtc] [datetime2](0) NOT NULL,
 CONSTRAINT [PK_ProjectNotes] PRIMARY KEY CLUSTERED 
(
	[ProjectNoteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[ProjectNotes] ADD  CONSTRAINT [DF_ProjectNotes_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO

ALTER TABLE [dbo].[ProjectNotes]  WITH CHECK ADD  CONSTRAINT [FK_ProjectNotes_CreatedBy] FOREIGN KEY([CreatedByUserId])
REFERENCES [dbo].[Users] ([UserId])
GO

ALTER TABLE [dbo].[ProjectNotes] CHECK CONSTRAINT [FK_ProjectNotes_CreatedBy]
GO

ALTER TABLE [dbo].[ProjectNotes]  WITH CHECK ADD  CONSTRAINT [FK_ProjectNotes_Posts] FOREIGN KEY([PostId])
REFERENCES [dbo].[Posts] ([PostId])
GO

ALTER TABLE [dbo].[ProjectNotes] CHECK CONSTRAINT [FK_ProjectNotes_Posts]
GO


