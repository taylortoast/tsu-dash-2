USE [TSU-Dashboard]
GO

/****** Object:  Table [dbo].[Users]    Script Date: 9/1/2026 6:36:43 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Users](
	[UserId] [int] IDENTITY(1,1) NOT NULL,
	[WindowsUserName] [nvarchar](150) NOT NULL,
	[DisplayName] [nvarchar](150) NULL,
	[AssignedSectionId] [int] NULL,
	[IsActive] [bit] NOT NULL,
	[IsAdmin] [bit] NOT NULL,
	[FirstSeenUtc] [datetime2](0) NOT NULL,
	[LastLoginUtc] [datetime2](0) NULL,
	[CreatedUtc] [datetime2](0) NOT NULL,
	[UpdatedUtc] [datetime2](0) NOT NULL,
	[CanAccessAssignmentsBoard] [bit] NOT NULL,
	[IsTsuiAdmin] [bit] NOT NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_Users_WindowsUserName] UNIQUE NONCLUSTERED 
(
	[WindowsUserName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_IsActive]  DEFAULT ((0)) FOR [IsActive]
GO

ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_IsAdmin]  DEFAULT ((0)) FOR [IsAdmin]
GO

ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_FirstSeenUtc]  DEFAULT (sysutcdatetime()) FOR [FirstSeenUtc]
GO

ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_CreatedUtc]  DEFAULT (sysutcdatetime()) FOR [CreatedUtc]
GO

ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_UpdatedUtc]  DEFAULT (sysutcdatetime()) FOR [UpdatedUtc]
GO

ALTER TABLE [dbo].[Users] ADD  CONSTRAINT [DF_Users_CanAccessAssignmentsBoard]  DEFAULT ((0)) FOR [CanAccessAssignmentsBoard]
GO

ALTER TABLE [dbo].[Users]  WITH CHECK ADD  CONSTRAINT [FK_Users_Sections] FOREIGN KEY([AssignedSectionId])
REFERENCES [dbo].[Sections] ([SectionId])
GO

ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [FK_Users_Sections]
GO


