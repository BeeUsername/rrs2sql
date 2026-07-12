ALTER TABLE [Order] 
ALTER COLUMN IsEmulatorRegistrar BIT NULL;

GO
CREATE TRIGGER IsEmulatorRegistrar_INSERT_UPDATE
   ON  [Order] 
   AFTER  INSERT,UPDATE
AS 
UPDATE [Order]
SET IsEmulatorRegistrar = 0
where IsEmulatorRegistrar is null
GO