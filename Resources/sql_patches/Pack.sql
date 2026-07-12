
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[BmsCardNum](
	[Id] [bigint] NOT NULL,
	[CreatedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[BmsCardNum] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO

CREATE TABLE [dbo].[BmsPaymentTransaction](
	[RRN] [nvarchar](50) NOT NULL,
	[CreatedAt] [datetime] NULL,
	[OrderCode] [nvarchar](50) NULL,
	[Json] [ntext] NULL,
	[IsCompleted] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[RRN] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

ALTER TABLE [dbo].[BmsPaymentTransaction] ADD  DEFAULT ((0)) FOR [IsCompleted]
GO

CREATE TABLE [dbo].[BonusGroup](
	[Id] [bigint] NOT NULL,
	[Json] [ntext] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

CREATE TABLE [dbo].[BonusGroupTarget](
	[Id] [bigint] NULL,
	[BonusGroupId] [bigint] NULL
) ON [PRIMARY]

GO

CREATE TABLE [dbo].[Category](
	[Id] [bigint] NOT NULL,
	[ParentId] [bigint] NULL,
	[Name] [nvarchar](300) NOT NULL,
	[SortOrder] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

CREATE TABLE [dbo].[Exam](
	[Id] [bigint] NOT NULL,
	[PdfResultId] [int] NULL,
	[RecommendedExamCodes] [ntext] NULL,
	[Name] [nvarchar](500) NULL,
	[SecondCode] [ntext] NULL,
	[BodyAreaName] [nvarchar](80) NULL,
	[MaterialName] [ntext] NULL,
	[SampleName] [ntext] NULL,
	[Preparing] [ntext] NULL,
	[SamplingRules] [ntext] NULL,
	[ContainerName] [nvarchar](500) NULL,
	[SampleCentrifugation] [nvarchar](1000) NULL,
	[SampleStorageConditions] [nvarchar](1000) NULL,
	[SampleStabilityDuration] [nvarchar](300) NULL,
	[Notes] [ntext] NULL,
	[AlternativeName] [nvarchar](2000) NULL,
	[EnglishName] [nvarchar](1000) NULL,
	[TransportationConditions] [nvarchar](max) NULL,
	[CatalogId] [bigint] NULL,
	[ExecuteLocally] [bit] NULL,
	[FasonCode] [nvarchar](100) NULL,
	[SampleMinVolumeMkl] [float] NULL,
	[SampleContainerNullVolume] [float] NULL,
	[SampleAddContanierPerVolume] [bit] NULL,
	[SampleAddContanierPerLab] [bit] NULL,
	[ServiceTestCode] [ntext] NULL,
	[ContainerCodes] [ntext] NULL,
	[MaterialCode] [nvarchar](100) NULL,
	[Cito] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

ALTER TABLE [dbo].[Exam] ADD  DEFAULT ((0)) FOR [Cito]
GO

CREATE TABLE [dbo].[HospitalSetting](
	[Id] [bigint] NOT NULL,
	[HospitalSettingInfo] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

CREATE TABLE [dbo].[InquiriesMod](
	[Id] [bigint] NOT NULL,
	[InquiriesMod] [ntext] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

CREATE TABLE [dbo].[OrderMod](
	[Id] [bigint] NOT NULL,
	[OrderMod] [ntext] NULL,
	[CodeNum] [bigint] NULL,
	[PreOrderId] [bigint] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

ALTER TABLE [dbo].[OrderMod] ADD  DEFAULT ((0)) FOR [CodeNum]
GO

ALTER TABLE [dbo].[OrderMod] ADD  DEFAULT ((0)) FOR [PreOrderId]
GO

CREATE TABLE [dbo].[OrderRecieptMod](
	[Id] [uniqueidentifier] NOT NULL,
	[Json] [ntext] NULL,
	[CreatedAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

ALTER TABLE [dbo].[OrderRecieptMod] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO

CREATE TABLE [dbo].[SbpPaymentTransaction](
	[RRN] [nvarchar](100) NOT NULL,
	[OrderCode] [nvarchar](100) NOT NULL,
	[PaymentSum] [money] NOT NULL,
	[IsRepay] [bit] NOT NULL,
	[Patient] [nvarchar](100) NOT NULL,
	[Inn] [nvarchar](100) NOT NULL,
	[Kpp] [nvarchar](100) NOT NULL,
	[FrSerialNumber] [nvarchar](100) NOT NULL,
	[QrId] [nvarchar](100) NULL,
	[CreatedAt] [datetime] NOT NULL,
	[CompletedAt] [datetime] NULL,
	[IsRemoved] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[RRN] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

CREATE TABLE [dbo].[Promocode](
	[Promocode] [nvarchar](150) NOT NULL,
	[PromocodeInfo] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED 
(
	[Promocode] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

CREATE TABLE [dbo].[Prefix](
	[id] [int] NOT NULL,
	[value] [nvarchar](3) NOT NULL,
	[description] [nvarchar](max) NOT NULL,
	[removed] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO

CREATE TRIGGER IsEmulatorRegistrar_INSERT_UPDATE
   ON  [Order] 
   AFTER  INSERT,UPDATE
AS 
UPDATE [Order]
SET IsEmulatorRegistrar = 0
where IsEmulatorRegistrar is null
GO