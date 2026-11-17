using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

[AllowAnonymous]
public class TrackController : AppController
{
    private readonly BillingService _billing;
    public TrackController(AppDbContext db, CurrentUser me, BillingService billing) : base(db, me) { _billing = billing; }

public async Task<IActionResult> Index(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return View("Index", new TrackVm());

        var kind = IdFormat.Detect(id);
        if (kind == IdKind.Order) return await OrderResult(IdFormat.Normalize(id));
        if (kind == IdKind.Account) return await ConnectionResult(IdFormat.Normalize(id));

        return View("Index", new TrackVm
        {
            Query = id,
            Message = "That is not an order id (a letter and 10 digits) or an account id (a letter and 15 digits)"
        });
    }

    public Task<IActionResult> Order(string id) => OrderResult(IdFormat.Normalize(id));
    public Task<IActionResult> Connection(string id) => ConnectionResult(IdFormat.Normalize(id));

    private async Task<IActionResult> OrderResult(string orderNo)
    {
        if (Me.IsAuthenticated)
        {
            var mine = await Db.ServiceOrders.AsNoTracking().ForUser(Me).FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (mine != null) return RedirectToAction("Details", "Orders", new { id = mine.Id });
        }
        var order = await Db.ServiceOrders.AsNoTracking()
            .Include(o => o.Plan).Include(o => o.InstallCity).Include(o => o.FeasibilityChecks)
            .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
        if (order == null)
            return View("Index", new TrackVm { Query = orderNo, Message = "No order has that id. Check the characters on your receipt" });
        return View("Order", new TrackVm { Query = orderNo, Order = order });
    }

    private async Task<IActionResult> ConnectionResult(string accountId)
    {
        if (Me.IsAuthenticated)
        {
            var mine = await Db.Connections.AsNoTracking().ForUser(Me).FirstOrDefaultAsync(x => x.AccountId == accountId);
            if (mine != null) return RedirectToAction("Details", "Connections", new { id = mine.Id });
        }
        var c = await Db.Connections.AsNoTracking()
            .Include(x => x.Plan).Include(x => x.City).Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.AccountId == accountId);
        if (c == null)
            return View("Index", new TrackVm { Query = accountId, Message = "No connection has that account id. Check the characters on your bill" });

        var bills = await Db.Bills.AsNoTracking().Where(b => b.ConnectionId == c.Id && !b.CarriedForward).ToListAsync();
        var due = bills.Sum(b => b.Total - b.AmountPaid);
        var open = bills.Where(b => b.Total - b.AmountPaid > 0).OrderBy(b => b.DueDate).FirstOrDefault();
        return View("Connection", new TrackVm
        {
            Query = accountId,
            Connection = c,
            Due = due,
            NextDue = open?.DueDate,
            Overdue = open != null && open.DueDate < DateTime.Today
        });
    }
}
