using Nexus.Web.Models;

namespace Nexus.Web.ViewModels;

/// <summary>One class for every role's start page. Each role fills in only its own part.</summary>
public class DashboardVm
{
    public string Role { get; set; }
    public string ShopName { get; set; }

    // Counters
    public int OpenOrders { get; set; }
    public int ActiveConnections { get; set; }
    public int Employees { get; set; }
    public int ToCheck { get; set; }
    public int ReadyToConnect { get; set; }
    public int AwaitingBill { get; set; }
    public int OverdueCount { get; set; }
    public decimal OutstandingTotal { get; set; }
    public decimal BilledThisMonth { get; set; }
    public decimal CollectedThisMonth { get; set; }
    public decimal CollectedToday { get; set; }
    public decimal DueNow { get; set; }

    // Lists
    public List<ServiceOrder> Orders { get; set; } = new();
    public List<Connection> Connections { get; set; } = new();
    public List<Bill> Bills { get; set; } = new();
    public List<StockLine> Stock { get; set; } = new();
    public List<Feedback> Feedback { get; set; } = new();
    public List<Connection> PendingRefunds { get; set; } = new();
    public Dictionary<int, decimal> DueByConnection { get; set; } = new();
}

/// <summary>Stock against the orders still waiting for equipment.</summary>
public class StockLine
{
    public Product Product { get; set; }
    public int Demand { get; set; }
    public int Shortfall => Math.Max(0, Demand - Product.StockQty);
    public bool Low => Product.StockQty <= Product.ReorderLevel;
}
