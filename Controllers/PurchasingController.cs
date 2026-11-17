using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

/// <summary>Purchase orders to equipment vendors. Receiving one adds the units to stock.</summary>
[Authorize(Roles = Roles.Admin)]
public class PurchasingController : AppController
{
    private readonly SequenceService _seq;
    public PurchasingController(AppDbContext db, CurrentUser me, SequenceService seq) : base(db, me) { _seq = seq; }

    public async Task<IActionResult> Index(PurchaseStatus? status, int page = 1)
    {
        var query = Db.PurchaseOrders.AsNoTracking().Include(p => p.Vendor).Include(p => p.Lines).AsQueryable();
        if (status != null) query = query.Where(p => p.Status == status);
        ViewBag.Status = status;
        return View(await Paged<PurchaseOrder>.CreateAsync(query.OrderByDescending(p => p.OrderedOn).ThenByDescending(p => p.Id), page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var po = await Db.PurchaseOrders.AsNoTracking().Include(p => p.Vendor)
            .Include(p => p.Lines).ThenInclude(l => l.Product).FirstOrDefaultAsync(p => p.Id == id);
        return po == null ? NotFound() : View(po);
    }

    public async Task<IActionResult> Create(int? productId)
    {
        var vm = await FormAsync(new PurchaseFormVm());
        if (productId != null)
        {
            var p = vm.Products.FirstOrDefault(x => x.Id == productId);
            if (p != null) { vm.VendorId = p.VendorId; vm.Lines.Add(new PurchaseLineVm { ProductId = p.Id, Quantity = 10 }); }
        }
        if (vm.Lines.Count == 0) vm.Lines.Add(new PurchaseLineVm { Quantity = 1 });
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(PurchaseFormVm vm)
    {
        vm.Lines = (vm.Lines ?? new()).Where(l => l.ProductId != 0 && l.Quantity > 0).ToList();
        if (vm.Lines.Count == 0) ModelState.AddModelError("", "Add at least one product with a quantity");
        if (vm.Lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1)) ModelState.AddModelError("", "Each product can appear once");
        if (!ModelState.IsValid) { await FormAsync(vm); if (vm.Lines.Count == 0) vm.Lines.Add(new PurchaseLineVm { Quantity = 1 }); return View(vm); }

        var po = new PurchaseOrder
        {
            PoNumber = await _seq.PurchaseNoAsync(),
            VendorId = vm.VendorId.Value,
            Notes = vm.Notes?.Trim(),
            CreatedById = Me.Id,
            Lines = vm.Lines.Select(l => new PurchaseOrderLine { ProductId = l.ProductId, Quantity = l.Quantity, UnitCost = l.UnitCost }).ToList()
        };
        Db.PurchaseOrders.Add(po);
        await Db.SaveChangesAsync();
        Flash("Purchase order " + po.PoNumber + " created");
        return RedirectToAction(nameof(Details), new { id = po.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Receive(int id)
    {
        var po = await Db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id);
        if (po == null) return NotFound();
        if (po.Status != PurchaseStatus.Ordered) { Flash("This purchase order is already closed", "error"); return RedirectToAction(nameof(Details), new { id }); }
        foreach (var line in po.Lines)
        {
            var product = await Db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId);
            if (product != null) product.StockQty += line.Quantity;
        }
        po.Status = PurchaseStatus.Received;
        po.ReceivedOn = DateTime.Today;
        await Db.SaveChangesAsync();
        Flash("Stock updated from " + po.PoNumber);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        var po = await Db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == id);
        if (po == null) return NotFound();
        if (po.Status != PurchaseStatus.Ordered) { Flash("This purchase order is already closed", "error"); return RedirectToAction(nameof(Details), new { id }); }
        po.Status = PurchaseStatus.Cancelled;
        await Db.SaveChangesAsync();
        Flash("Purchase order cancelled");
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<PurchaseFormVm> FormAsync(PurchaseFormVm vm)
    {
        vm.Vendors = await Db.Vendors.AsNoTracking().Where(v => v.IsActive).OrderBy(v => v.Name).ToListAsync();
        vm.Products = await Db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        return vm;
    }
}
