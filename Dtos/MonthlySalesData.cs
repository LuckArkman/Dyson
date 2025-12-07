namespace Dtos;

public class MonthlySalesData
{
    public string MonthLabel { get; set; } // Ex: "Jan/2024"
    public int Count { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
}