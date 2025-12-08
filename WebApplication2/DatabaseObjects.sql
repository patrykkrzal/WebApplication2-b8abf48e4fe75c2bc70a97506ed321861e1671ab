-- Funkcja sumuj¹ca wartoœæ zamówienia
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

-- Procedura dodaj¹ca sprzêt
IF OBJECT_ID('dbo.spAddEquipment','P') IS NOT NULL
 DROP PROCEDURE dbo.spAddEquipment;
GO
CREATE PROCEDURE dbo.spAddEquipment
 @Type INT,
 @Size INT,
 @Price DECIMAL(18,2)
AS
BEGIN
 SET NOCOUNT ON;
 INSERT INTO Equipment (Type, Size, Is_In_Werehouse, Price, Is_Reserved)
 VALUES (@Type, @Size,1, @Price,0);

 SELECT SCOPE_IDENTITY() AS NewEquipmentId;
END;
GO

IF OBJECT_ID('dbo.trg_OrderedItems_AfterInsert','TR') IS NOT NULL
 DROP TRIGGER dbo.trg_OrderedItems_AfterInsert;
GO

GO

-- Pricing objects
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

 --  gives5%, then subtract5%, cap at20%
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

-- Procedure to add order using calculation
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

CREATE OR ALTER TRIGGER dbo.trg_Orders_ValidateDiscount
ON Orders
AFTER INSERT
AS
BEGIN
 SET NOCOUNT ON;
END
GO
