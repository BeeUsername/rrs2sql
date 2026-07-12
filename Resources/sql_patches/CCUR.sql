CREATE FUNCTION [dbo].[CCUR](@code nvarchar(255)) 
RETURNS decimal
AS
BEGIN
 RETURN CAST(@code as decimal)
END
