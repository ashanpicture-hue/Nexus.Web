using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

public class PaymentsController : AppController
{
    private readonly BillingService _billing;
    public PaymentsController(AppDbContext db, CurrentUser me, BillingService billing) : base(db, me) { _billing = billing; }

    public async Task<IActionResult> Index(int page = 1)
    {
        var query = Db.Payments.AsNoTracking().ForUser(Me)
            .Include(p => p.Bill).ThenInclude(b => b.Connection).ThenInclude(c => c.Customer)
            .Include(p => p.RetailShop);
        return View(await Paged<Payment>.CreateAsync(query.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.Id), page));
    }

    [Authorize(Roles = Roles.Retail + "," + Roles.Accounts)]
    public async Task<IActionResult> Find(string accountId)
    {
        var id = IdFormat.Normalize(accountId);
        var c = await Db.Connections.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == id);
        if (c == null)
        {
            Flash("No connection has that account id", "error");
            return RedirectToAction(nameof(Index));
        }
        var bill = await Db.Bills.AsNoTracking()
            .Where(b => b.ConnectionId == c.Id && !b.CarriedForward && b.Total > b.AmountPaid)
            .OrderBy(b => b.DueDate).FirstOrDefaultAsync();
        if (bill == null)
        {
            Flash("Nothing is due on this connection", "ok");
            return RedirectToAction("Details", "Connections", new { id = c.Id });
        }
        return RedirectToAction(nameof(Create), new { billId = bill.Id });
    }

    [Authorize(Roles = Roles.Retail + "," + Roles.Accounts)]
    public async Task<IActionResult> Create(int billId)
    {
        var bill = await LoadBillAsync(billId);
        if (bill == null) return NotFound();
        ViewBag.Bill = bill;
        return View(new PaymentVm { BillId = billId, Amount = bill.Due });
    }

    [HttpPost, Authorize(Roles = Roles.Retail + "," + Roles.Accounts)]
    public async Task<IActionResult> Create(PaymentVm vm)
    {
        var bill = await LoadBillAsync(vm.BillId);
        if (bill == null) return NotFound();
        ViewBag.Bill = bill;
        if (!ModelState.IsValid) return View(vm);

        int? shop = Me.Role == Roles.Retail ? Me.ShopId : null;
        var (payment, err) = await _billing.PayAsync(vm.BillId, vm.Amount, vm.Mode, vm.Reference, vm.PaidOn, Me.Id, shop);
        if (err != null)
        {
            ModelState.AddModelError("", err);
            return View(vm);
        }
        Flash("Payment recorded, receipt " + payment.ReceiptNo);
        return RedirectToAction("Receipt", new { id = payment.Id });
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var p = await Db.Payments.AsNoTracking().ForUser(Me)
            .Include(x => x.Bill).ThenInclude(b => b.Connection).ThenInclude(c => c.Customer)
            .Include(x => x.RetailShop)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        ViewBag.ReceivedBy = await Db.Users.AsNoTracking().Where(u => u.Id == p.ReceivedById).Select(u => u.FullName).FirstOrDefaultAsync();
        return View(p);
    }

    private Task<Bill> LoadBillAsync(int id) =>
        Db.Bills.AsNoTracking().Include(b => b.Connection).ThenInclude(c => c.Customer).FirstOrDefaultAsync(b => b.Id == id);
}
