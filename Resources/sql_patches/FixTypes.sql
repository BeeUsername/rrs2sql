

DECLARE @Query NVARCHAR(MAX);

SELECT @Query = 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' FROM sys.key_constraints WHERE [type] = 'PK' AND [parent_object_id] = OBJECT_ID('Category');
IF @Query IS NOT NULL
BEGIN
    EXEC sp_executesql @Query;
END

GO

ALTER TABLE [dbo].[Category]
ALTER COLUMN 
	[Id] [bigint] NOT NULL

GO

ALTER TABLE [dbo].[Category]
ALTER COLUMN 
	[ParentId] [bigint] NULL

GO

ALTER TABLE [dbo].[Category] ADD PRIMARY KEY ([Id])

GO

DECLARE @Query NVARCHAR(MAX);

SELECT @Query = 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' FROM sys.key_constraints WHERE [type] = 'PK' AND [parent_object_id] = OBJECT_ID('ExamTemporaryChanges');
IF @Query IS NOT NULL
BEGIN
    EXEC sp_executesql @Query;
END

GO

ALTER TABLE [dbo].[ExamTemporaryChanges]
ALTER COLUMN 
	[Id] [bigint] NOT NULL

GO

ALTER TABLE [dbo].[ExamTemporaryChanges]
ALTER COLUMN 
	[ExamId] [bigint] NOT NULL

GO

ALTER TABLE [dbo].[ExamTemporaryChanges] ADD PRIMARY KEY ([Id])

GO

DECLARE @Query NVARCHAR(MAX);

SELECT @Query = 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' FROM sys.key_constraints WHERE [type] = 'PK' AND [parent_object_id] = OBJECT_ID('BonusGroup');
IF @Query IS NOT NULL
BEGIN
    EXEC sp_executesql @Query;
END

GO

ALTER TABLE [dbo].[BonusGroup]
ALTER COLUMN 
	[Id] [bigint] NOT NULL

GO

ALTER TABLE [dbo].[BonusGroup] ADD PRIMARY KEY ([Id])

GO

ALTER TABLE [dbo].[BonusGroupTarget]
ALTER COLUMN 
	[Id] [bigint] NULL

GO

ALTER TABLE [dbo].[BonusGroupTarget]
ALTER COLUMN 
	[BonusGroupId] [bigint] NULL

GO

DECLARE @Query NVARCHAR(MAX);

SELECT @Query = 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' FROM sys.key_constraints WHERE [type] = 'PK' AND [parent_object_id] = OBJECT_ID('OrderMod');
IF @Query IS NOT NULL
BEGIN
    EXEC sp_executesql @Query;
END

GO

ALTER TABLE [dbo].[OrderMod]
ALTER COLUMN 
	[Id] [bigint] NOT NULL

GO

ALTER TABLE [dbo].[OrderMod] ADD PRIMARY KEY ([Id])

GO

DECLARE @Query NVARCHAR(MAX);

SELECT @Query = 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' FROM sys.key_constraints WHERE [type] = 'PK' AND [parent_object_id] = OBJECT_ID('BmsCardNum');
IF @Query IS NOT NULL
BEGIN
    EXEC sp_executesql @Query;
END

GO

ALTER TABLE [dbo].[BmsCardNum]
ALTER COLUMN 
	[Id] [bigint] NOT NULL

GO

ALTER TABLE [dbo].[BmsCardNum] ADD PRIMARY KEY ([Id])

GO

DECLARE @ResetVersion BIT;
DECLARE @ID BIGINT;

SET @ResetVersion = 0

DECLARE InquiriesCursor CURSOR FOR SELECT [Id] FROM [dbo].[InquiriesMod] GROUP BY [Id] HAVING COUNT(*) > 1;

OPEN InquiriesCursor

FETCH NEXT FROM InquiriesCursor INTO @ID
WHILE @@FETCH_STATUS = 0
BEGIN

	SET @ResetVersion = 1

	DELETE FROM [dbo].[InquiriesMod] WHERE [Id] = @ID
    FETCH NEXT FROM InquiriesCursor INTO @ID
END

CLOSE InquiriesCursor;
DEALLOCATE InquiriesCursor;

IF @ResetVersion = 1
BEGIN
	UPDATE [ImportVersions] SET [Version] = 0 WHERE [DataCode] = 'containers';
END

GO

DECLARE @Query NVARCHAR(MAX);

SELECT @Query = 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' FROM sys.key_constraints WHERE [type] = 'PK' AND [parent_object_id] = OBJECT_ID('InquiriesMod');
IF @Query IS NOT NULL
BEGIN
    EXEC sp_executesql @Query;
END

GO

ALTER TABLE [dbo].[InquiriesMod]
ALTER COLUMN 
	[Id] [bigint] NOT NULL

GO

ALTER TABLE [dbo].[InquiriesMod] ADD PRIMARY KEY ([Id])

GO

DECLARE @Query NVARCHAR(MAX);

SELECT @Query = 'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' FROM sys.key_constraints WHERE [type] = 'PK' AND [parent_object_id] = OBJECT_ID('Exam');
IF @Query IS NOT NULL
BEGIN
    EXEC sp_executesql @Query;
END

GO

ALTER TABLE [dbo].[Exam]
ALTER COLUMN 
	[Id] [bigint] NOT NULL

GO

ALTER TABLE [dbo].[Exam]
ALTER COLUMN 
	[CatalogId] [bigint] NULL

ALTER TABLE [dbo].[Exam] ADD PRIMARY KEY ([Id])

GO
