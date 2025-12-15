-- sum order total
IF OBJECT_ID('dbo.ufn_OrderTotal','FN') IS NOT NULL
 DROP FUNCTION dbo.ufn_OrderTotal;
GO
CREATE FUNCTION dbo.ufn_OrderTotal(@OrderId INT)
RETURNS DECIMAL(18,2)
AS
BEGIN
 DECLARE @total DECIMAL(18,2);
 SELECT @total = ISNULL(SUM(oi.Quantity * oi.PriceWhenOrdered),0)
 FROM OrderedItems oi
 WHERE oi.OrderId = @OrderId;
 RETURN @total;
END;
GO

-- Remove old OrderedItems trigger if present
IF OBJECT_ID('dbo.trg_OrderedItems_RecalculateOrderTotal','TR') IS NOT NULL
 DROP TRIGGER dbo.trg_OrderedItems_RecalculateOrderTotal;
GO

-- order logs table
IF OBJECT_ID('dbo.OrderLogs','U') IS NULL
BEGIN
 CREATE TABLE dbo.OrderLogs
 (
 Id INT IDENTITY PRIMARY KEY,
 OrderId INT NOT NULL,
 Message NVARCHAR(4000) NOT NULL,
 LogDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
 );
END
GO

-- DROP legacy Orders price-calc trigger if present
IF OBJECT_ID('dbo.trg_Orders_CalculatePriceOnInsert','TR') IS NOT NULL
 DROP TRIGGER dbo.trg_Orders_CalculatePriceOnInsert;
GO

-- set due date trigger (after update)
CREATE OR ALTER TRIGGER dbo.trg_Orders_SetDueDate
ON Orders
AFTER UPDATE
AS
BEGIN
 SET NOCOUNT ON;

 -- Only proceed for rows where Days or OrderDate were changed (or Days now present)
 ;WITH Changed AS (
 SELECT i.Id, i.Days, i.OrderDate, d.Days AS OldDays, d.OrderDate AS OldOrderDate
 FROM inserted i
 LEFT JOIN deleted d ON i.Id = d.Id
 WHERE ( (i.Days IS NOT NULL AND (d.Days IS NULL OR i.Days <> d.Days))
 OR (i.OrderDate IS NOT NULL AND (d.OrderDate IS NULL OR i.OrderDate <> d.OrderDate)) )
 )
 -- Update DueDate for these orders (only if DueDate is NULL to avoid overwriting explicit values)
 UPDATE o
 SET DueDate = DATEADD(day, CASE WHEN c.Days IS NULL OR c.Days <1 THEN 7 ELSE c.Days END, c.OrderDate)
 FROM Orders o
 INNER JOIN Changed c ON o.Id = c.Id
 WHERE o.DueDate IS NULL;

 -- insert audit log
 INSERT INTO dbo.OrderLogs (OrderId, Message)
 SELECT ch.Id,
 CONCAT('DueDate set by trigger to ', FORMAT(DATEADD(day, CASE WHEN ch.Days IS NULL OR ch.Days <1 THEN 7 ELSE ch.Days END, ch.OrderDate), 'yyyy-MM-dd HH:mm:ss'))
 FROM (
 SELECT i.Id, i.Days, i.OrderDate, d.Days AS OldDays, d.OrderDate AS OldOrderDate
 FROM inserted i
 LEFT JOIN deleted d ON i.Id = d.Id
 WHERE ( (i.Days IS NOT NULL AND (d.Days IS NULL OR i.Days <> d.Days))
 OR (i.OrderDate IS NOT NULL AND (d.OrderDate IS NULL OR i.OrderDate <> d.OrderDate)) )
 ) ch;
END
GO

-- pricing
GO
CREATE OR ALTER FUNCTION dbo.fnOrderDiscount(@itemsCount int, @days int)
RETURNS decimal(5,2)
AS
BEGIN
 -- gives5%, then subtract5%, cap at20%
 DECLARE @itemsRaw decimal(5,2) = CAST(ISNULL(@itemsCount,0) *0.05 AS decimal(5,2));
 DECLARE @itemsPct decimal(5,2) = @itemsRaw -0.05;
 IF @itemsPct <0.00 SET @itemsPct =0.00;
 IF @itemsPct >0.20 SET @itemsPct =0.20;

 -- gives5%, then subtract5%, cap at20%
 DECLARE @daysRaw decimal(5,2) = CAST(ISNULL(@days,0) *0.05 AS decimal(5,2));
 DECLARE @daysPct decimal(5,2) = @daysRaw -0.05;
 IF @daysPct <0.00 SET @daysPct =0.00;
 IF @daysPct >0.20 SET @daysPct =0.20;

 DECLARE @total decimal(5,2) = @itemsPct + @daysPct;
 -- overall cap at40%
 IF @total >0.40 SET @total =0.40;
 RETURN @total;
END
GO

CREATE OR ALTER PROCEDURE dbo.spCalculateOrderPrice
 @basePrice decimal(18,2),
 @itemsCount int,
 @days int,
 @finalPrice decimal(18,2) OUTPUT,
 @discountPct decimal(5,2) OUTPUT
AS
BEGIN
 SET NOCOUNT ON;
 IF @itemsCount <0 SET @itemsCount =0;
 IF @days <1 SET @days =1;
 DECLARE @multiplied decimal(18,2) = @basePrice * @days;
 SET @discountPct = dbo.fnOrderDiscount(@itemsCount, @days);
 SET @finalPrice = @multiplied * (1 - @discountPct);
END
GO

-- create order sp
CREATE OR ALTER PROCEDURE dbo.spCreateOrder
 @userId nvarchar(450),
 @rentedItems nvarchar(255),
 @basePrice decimal(18,2),
 @itemsCount int,
 @days int
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @final decimal(18,2), @pct decimal(5,2);
 EXEC dbo.spCalculateOrderPrice @basePrice, @itemsCount, @days, @final OUTPUT, @pct OUTPUT;
 INSERT INTO Orders (Rented_Items, OrderDate, Price, Date_Of_submission, Was_It_Returned, UserId)
 VALUES (@rentedItems, SYSUTCDATETIME(), @final, CAST(GETUTCDATE() AS date),0, @userId);
END
GO
