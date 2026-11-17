using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

/// <summary>Bills. Only the accounts department generates them; everyone else with access reads them.</summary>
public class BillingController : AppController
{
    private readonly BillingService _billing;
    private readonly SettingsService _settings;

    public BillingController(AppDbContext db, CurrentUser me, BillingService billing, SettingsService settings) : base(db, me)
    {
        _billing = billing; _settings = settings;
    }

    public async Task<IActionResult> Index(BillStatus? status, bool overdue, string q, int page = 1)
    {
        var query = Db.Bills.AsNoTracking().ForUser(Me)
            .Include(b => b.Connection).ThenInclude(c => c.Customer).AsQueryable();
        if (status != null) query = query.Where(b => b.Status == status);
        if (overdue) query = query.Where(b => !b.CarriedForward && b.Total > b.AmountPaid && b.DueDate < DateTime.Today);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var id = IdFormat.Normalize(q);
            query = query.Where(b => b.BillNo.Contains(q) || b.Connection.AccountId.Contains(id) || b.Connection.Customer.FullName.Contains(q));
        }
        ViewBag.Status = status; ViewBag.Overdue = overdue; ViewBag.Q = q;
        return View(await Paged<Bill>.CreateAsync(query.OrderByDescending(b => b.IssuedOn).ThenByDescending(b => b.Id), page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var bill = await Db.Bills.AsNoTracking().ForUser(Me)
            .Include(b => b.Connection).ThenInclude(c => c.Customer)
            .Include(b => b.Connection).ThenInclude(c => c.Plan)
            .Include(b => b.Payments).ThenInclude(p => p.RetailShop)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bill == null) return NotFound();
        return View(bill);
    }

    /// <summary>Connections that need their next bill.</summary>
    [Authorize(Roles = Roles.Accounts)]
    public async Task<IActionResult> Pending(int page = 1)
    {
        var today = DateTime.Today;
        var query = Db.Connections.AsNoTracking().Include(c => c.Customer).Include(c => c.Plan)
            .Where(c => c.Status != ConnectionStatus.PermanentlyInactive && !c.Bills.Any(b => b.PeriodEnd >= today))
            .OrderBy(c => c.ActivatedOn);
        return View(await Paged<Connection>.CreateAsync(query, page));
    }

    [Authorize(Roles = Roles.Accounts)]
    public async Task<IActionResult> Generate(int connectionId)
    {
        var vm = new BillFormVm { ConnectionId = connectionId };
        if (!await FillAsync(vm)) return NotFound();
        vm.PeriodStart = await _billing.NextPeriodStartAsync(connectionId);
        await PreviewAsync(vm);
        return View(vm);
    }

    /// <summary>Two buttons: Preview recalculates, Generate saves. A bill cannot be edited once saved.</summary>
    [HttpPost, Authorize(Roles = Roles.Accounts)]
    public async Task<IActionResult> Generate(BillFormVm vm)
    {
        if (!await FillAsync(vm)) return NotFound();
        if (!ModelState.IsValid) return View(vm);

        if (vm.Intent != "generate")
        {
            await PreviewAsync(vm);
            return View(vm);
        }

        var (bill, err) = await _billing.GenerateAsync(new GenerateBillRequest
        {
            ConnectionId = vm.ConnectionId,
            PeriodStart = vm.PeriodStart,
            LocalMinutes = vm.LocalMinutes,
            StdMinutes = vm.StdMinutes,
            MobileMsgMinutes = vm.MobileMsgMinutes,
            Notes = vm.Notes
        }, Me.Id);
        if (err != null)
        {
            ModelState.AddModelError("", err);
            await PreviewAsync(vm);
            return View(vm);
        }
        Flash("Bill " + bill.BillNo + " generated");
        return RedirectToAction(nameof(Details), new { id = bill.Id });
    }

    private async Task<bool> FillAsync(BillFormVm vm)
    {
        vm.Connection = await Db.Connections.AsNoTracking().Include(c => c.Customer).Include(c => c.Plan)
            .Include(c => c.Bills).FirstOrDefaultAsync(c => c.Id == vm.ConnectionId);
        if (vm.Connection == null) return false;
        vm.Outstanding = await _billing.OutstandingAsync(vm.ConnectionId);
        vm.PendingCharges = await Db.ConnectionCharges.AsNoTracking()
            .Where(x => x.ConnectionId == vm.ConnectionId && x.BilledOnBillId == null).ToListAsync();
        return true;
    }

    /// <summary>Works out the bill without saving it, using the same rules as generation.</summary>
    private async Task PreviewAsync(BillFormVm vm)
    {
        var c = vm.Connection;
        var plan = c.Plan;
        var deposit = await Db.DepositRates.AsNoTracking().FirstOrDefaultAsync(d => d.ConnectionType == c.ConnectionType);
        var openDue = c.Bills.Where(b => !b.CarriedForward).Sum(b => b.Total - b.AmountPaid);
        vm.Preview = Pricing.ComputeBill(new BillInput
        {
            PlanCharge = plan.Price,
            LocalMinutes = plan.LocalRate != null ? vm.LocalMinutes : 0,
            StdMinutes = plan.StdRate != null ? vm.StdMinutes : 0,
            MobileMsgMinutes = plan.MobileMsgRate != null ? vm.MobileMsgMinutes : 0,
            LocalRate = plan.LocalRate,
            StdRate = plan.StdRate,
            MobileMsgRate = plan.MobileMsgRate,
            ReplacementCharge = vm.PendingCharges.Sum(x => x.Amount),
            IsFirstBill = c.Bills.Count == 0,
            DepositGross = deposit?.Amount ?? 0,
            DiscountPercent = c.DiscountPercent,
            TaxPercent = await _settings.GetDecimalAsync(SettingKeys.TaxPercent),
            PreviousDue = openDue
        });
        vm.PreviewPeriodEnd = vm.PeriodStart.Date.AddMonths(plan.ValidityMonths).AddDays(-1);
    }
}
