UPDATE OrderTarget
SET
    DiscSum = 0,
    AbsDisc1Sum = 0,
    AbsDisc2Sum = 0,
    Disc2Sum = 0,
    PriceOrig = 0
	
WHERE DiscSum IS NULL
   OR AbsDisc1Sum IS NULL
   OR AbsDisc2Sum IS NULL
   OR PriceOrig IS NULL
   OR Disc2Sum IS NULL;