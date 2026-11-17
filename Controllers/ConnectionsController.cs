using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

/// <summary>Connections: everyone sees what their role allows; the technical team changes status, plan and equipment.</summary>
public class ConnectionsController : AppController
{
    private readonly ConnectionService _svc;
    private readonly BillingService _billing;

    public ConnectionsController(AppDbContext db, CurrentUser me, ConnectionService svc, BillingService billing) : base(db, me)
    {
        _svc = svc; _billing = billing;
    }

    public async Task<IActionResult> Index(ConnectionStatus? status, ConnectionType? type, string q, int page = 1)
    {
        var query = Db.Connections.AsNoTracking().ForUser(Me)
            .Include(c => c.Customer).Include(c => c.Plan).Include(c => c.City).AsQueryable();
        if (status != null) query = query.Where(c => c.Status == status);
        if (type != null) query = query.Where(c => c.ConnectionType == type);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var id = IdFormat.Normalize(q);
            query = query.Where(c => c.AccountId.Contains(id) || c.Customer.FullName.Contains(q));
        }
        ViewBag.Status = status; ViewBag.Type = type; ViewBag.Q = q;
        var paged = await Paged<Connection>.CreateAsync(query.OrderBy(c => c.AccountId), page);

        // Amount due per connection on this page, in one query.
        var ids = paged.Items.Select(c => c.Id).ToList();
        var bills = await Db.Bills.AsNoTracking().Where(b => ids.Contains(b.ConnectionId) && !b.CarriedForward).ToListAsync();
        ViewBag.Due = bills.GroupBy(b => b.ConnectionId).ToDictionary(g => g.Key, g => g.Sum(b => b.Total - b.AmountPaid));
        return View(paged);
    }

    public async Task<IActionResult> Details(int id)
    {
        var c = await Db.Connections.AsNoTracking().ForUser(Me)
            .Include(x => x.Customer).Include(x => x.Plan).Include(x => x.City)
            .Include(x => x.ServiceOrder)
            .Include(x => x.Equipment).ThenInclude(e => e.Product)
            .Include(x => x.Events).Include(x => x.Charges)
            .Include(x => x.Bills)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        ViewBag.Due = c.Bills.Where(b => !b.CarriedForward).Sum(b => b.Total - b.AmountPaid);
        if (Me.Role == Roles.Technical)
        {
            ViewBag.Plans = await Db.Plans.AsNoTracking().Where(p => p.IsActive && p.ConnectionType == c.ConnectionType && p.Id != c.PlanId)
                .OrderBy(p => p.Price).ToListAsync();
            ViewBag.Products = await Db.Products.AsNoTracking().Where(p => p.IsActive && p.StockQty > 0)
                .OrderBy(p => p.Name).ToListAsync();
        }
        return View(c);
    }

    [HttpPost, Authorize(Roles = Roles.Technical)]
    public async Task<IActionResult> Status(StatusChangeVm vm)
    {
        var err = await _svc.ChangeStatusAsync(vm.Id, vm.Target, vm.Reason, Me.Id);
        if (err != null) Flash(err, "error"); else Flash("Connection is now " + vm.Target.Label().ToLowerInvariant());
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost, Authorize(Roles = Roles.Technical)]
    public async Task<IActionResult> ChangePlan(PlanChangeVm vm)
    {
        var err = await _svc.ChangePlanAsync(vm.Id, vm.PlanId, Me.Id);
        if (err != null) Flash(err, "error"); else Flash("Plan changed. The next bill uses the new plan");
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost, Authorize(Roles = Roles.Technical)]
    public async Task<IActionResult> Replace(ReplaceEquipmentVm vm)
    {
        var err = await _svc.ReplaceEquipmentAsync(vm.Id, vm.ProductId, vm.Serial, vm.DamagedByCustomer, Me.Id);
        if (err != null) Flash(err, "error"); else Flash(vm.DamagedByCustomer ? "Equipment replaced. The charge goes on the next bill" : "Equipment replaced");
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    [HttpPost, Authorize(Roles = Roles.Accounts)]
    public async Task<IActionResult> Refund(int id)
    {
        var err = await _svc.MarkDepositRefundedAsync(id, Me.Id);
        if (err != null) Flash(err, "error"); else Flash("Deposit refund recorded");
        return RedirectToAction(nameof(Details), new { id });
    }
}
