using Dtos;

namespace Models;

public class DashboardViewModel
{
    public long TotalProducts { get; set; }
    public long TotalOrders { get; set; }
    public long TotalAdmins { get; set; }
    public List<Order> RecentOrders { get; set; } = new List<Order>();
}