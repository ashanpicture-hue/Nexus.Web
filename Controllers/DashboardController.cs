using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

/// <summary>Each role's start page: the things that role has to act on today.</summary>
public class DashboardController : AppController
{
    private static readonly OrderStatus[] OpenStatuses =
        { OrderStatus.Placed, OrderStatus.UnderFeasibilityCheck, OrderStatus.Feasible };

    public DashboardController(AppDbContext db, CurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index()
    {
        var vm = new DashboardVm { Role = Me.Role };
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        switch (Me.Role)
        {
            case Roles.Admin:
                vm.OpenOrders = await Db.ServiceOrders.CountAsync(o => OpenStatuses.Contains(o.Status));
                vm.ActiveConnections = await Db.Connections.CountAsync(c => c.Status == ConnectionStatus.Active);
                vm.Employees = await Db.Employees.CountAsync(e => e.User.IsActive);
                vm.Stock = await StockLinesAsync(onlyAttention: true);
                vm.Feedback = await Db.Feedbacks.AsNoTracking().Include(f => f.Customer)
                    .OrderByDescending(f => f.CreatedOn).Take(4).ToListAsync();
                vm.Orders = await Db.ServiceOrders.AsNoTracking().Include(o => o.Customer).Include(o => o.Plan)
                    .OrderByDescending(o => o.PlacedOn).Take(6).ToListAsync();
                break;

            case Roles.Technical:
                vm.ToCheck = await Db.ServiceOrders.CountAsync(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.UnderFeasibilityCheck);
                vm.ReadyToConnect = await Db.ServiceOrders.CountAsync(o => o.Status == OrderStatus.Feasible);
                vm.ActiveConnections = await Db.Connections.CountAsync(c => c.Status == ConnectionStatus.Active);
                vm.Orders = await Db.ServiceOrders.AsNoTracking().Include(o => o.Customer).Include(o => o.Plan)
                    .Where(o => o.Status == OrderStatus.Placed || o.Status == OrderStatus.UnderFeasibilityCheck || o.Status == OrderStatus.Feasible)
                    .OrderBy(o => o.PlacedOn).Take(8).ToListAsync();
                vm.Stock = await StockLinesAsync(onlyAttention: true);
                // Connections with a bill past its due date: candidates for suspension.
                vm.Connections = await Db.Connections.AsNoTracking().Include(c => c.Customer)
                    .Where(c => c.Status == ConnectionStatus.Active &&
                                c.Bills.Any(b => !b.CarriedForward && b.Total > b.AmountPaid && b.DueDate < today))
                    .OrderBy(c => c.AccountId).Take(6).ToListAsync();
                break;

            case Roles.Accounts:
                vm.AwaitingBill = await DueForBillingQuery(today).CountAsync();
                var openBills = Db.Bills.Where(b => !b.CarriedForward && b.Total > b.AmountPaid);
                vm.OutstandingTotal = (await openBills.Select(b => b.Total - b.AmountPaid).ToListAsync()).Sum();
                vm.OverdueCount = await openBills.CountAsync(b => b.DueDate < today);
                vm.BilledThisMonth = (await Db.Bills.Where(b => b.IssuedOn >= monthStart).Select(b => b.Total).ToListAsync()).Sum();
                vm.CollectedThisMonth = (await Db.Payments.Where(p => p.PaidOn >= monthStart).Select(p => p.Amount).ToListAsync()).Sum();
                vm.Connections = await DueForBillingQuery(today).AsNoTracking()
                    .Include(c => c.Customer).Include(c => c.Plan).OrderBy(c => c.ActivatedOn).Take(8).ToListAsync();
                vm.Bills = await Db.Bills.AsNoTracking().Include(b => b.Connection).ThenInclude(c => c.Customer)
                    .Where(b => !b.CarriedForward && b.Total > b.AmountPaid && b.DueDate < today)
                    .OrderBy(b => b.DueDate).Take(6).ToListAsync();
                vm.PendingRefunds = await Db.Connections.AsNoTracking().Include(c => c.Customer)
                    .Where(c => c.DepositRefundAmount != null && c.DepositRefundAmount > 0 && c.DepositRefundedOn == null)
                    .Take(5).ToListAsync();
                break;

            case Roles.Retail:
                var shopId = Me.ShopId ?? -1;
                var shop = await Db.RetailShops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shopId);
                vm.ShopName = shop?.Name;
                vm.OpenOrders = await Db.ServiceOrders.CountAsync(o => o.RetailShopId == shopId && OpenStatuses.Contains(o.Status));
                vm.ActiveConnections = await Db.Connections.CountAsync(c => c.ServiceOrder.RetailShopId == shopId && c.Status == ConnectionStatus.Active);
                vm.CollectedToday = (await Db.Payments.Where(p => p.RetailShopId == shopId && p.PaidOn == today).Select(p => p.Amount).ToListAsync()).Sum();
                vm.Orders = await Db.ServiceOrders.AsNoTracking().Include(o => o.Customer).Include(o => o.Plan)
                    .Where(o => o.RetailShopId == shopId).OrderByDescending(o => o.PlacedOn).Take(8).ToListAsync();
                break;

            default: // Customer
                var cid = Me.CustomerId ?? -1;
                vm.Connections = await Db.Connections.AsNoTracking().Include(c => c.Plan).Include(c => c.City)
                    .Where(c => c.CustomerId == cid).OrderBy(c => c.AccountId).ToListAsync();
                vm.Orders = await Db.ServiceOrders.AsNoTracking().Include(o => o.Plan)
                    .Where(o => o.CustomerId == cid && o.Status != OrderStatus.ConnectionProvided)
                    .OrderByDescending(o => o.PlacedOn).Take(6).ToListAsync();
                var bills = await Db.Bills.AsNoTracking()
                    .Where(b => b.Connection.CustomerId == cid && !b.CarriedForward && b.Total > b.AmountPaid).ToListAsync();
                vm.DueNow = bills.Sum(b => b.Total - b.AmountPaid);
                vm.DueByConnection = bills.GroupBy(b => b.ConnectionId).ToDictionary(g => g.Key, g => g.Sum(b => b.Total - b.AmountPaid));
                vm.OverdueCount = bills.Count(b => b.DueDate < today);
                break;
        }
        return View(vm);
    }

    /// <summary>Connections that have no bill covering today or later, so they are due for the next bill.</summary>
    public IQueryable<Connection> DueForBillingQuery(DateTime today) =>
        Db.Connections.Where(c => c.Status != ConnectionStatus.PermanentlyInactive &&
                                  !c.Bills.Any(b => b.PeriodEnd >= today));

    /// <summary>Stock beside the orders still waiting for it. With onlyAttention, only low or short lines.</summary>
    private async Task<List<StockLine>> StockLinesAsync(bool onlyAttention)
    {
        var products = await Db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        var demand = await Db.ServiceOrders
            .Where(o => o.ProductId != null && OpenStatuses.Contains(o.Status))
            .GroupBy(o => o.ProductId.Value)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();
        var lines = products.Select(p => new StockLine
        {
            Product = p,
            Demand = demand.Where(d => d.ProductId == p.Id).Select(d => d.Qty).FirstOrDefault()
        }).ToList();
        return onlyAttention ? lines.Where(l => l.Low || l.Shortfall > 0).ToList() : lines;
    }
}
