using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class ShopsController : AppController
{
    public ShopsController(AppDbContext db, CurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index(string q)
    {
        var query = Db.RetailShops.AsNoTracking().Include(s => s.City).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(s => s.Name.Contains(q) || s.City.Name.Contains(q));
        ViewBag.Q = q;
        return View(await query.OrderBy(s => s.City.Name).ThenBy(s => s.Name).ToListAsync());
    }

    public async Task<IActionResult> Create() { ViewBag.Cities = await CityOptionsAsync(); return View("Form", new RetailShop()); }

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,CityId,Address,Phone,IsActive")] RetailShop m)
    {
        ViewBag.Cities = await CityOptionsAsync(m.CityId);
        if (!ModelState.IsValid) return View("Form", m);
        Db.RetailShops.Add(m);
        await Db.SaveChangesAsync();
        Flash("Shop added");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var m = await Db.RetailShops.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (m == null) return NotFound();
        ViewBag.Cities = await CityOptionsAsync(m.CityId);
        return View("Form", m);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Name,CityId,Address,Phone,IsActive")] RetailShop m)
    {
        var e = await Db.RetailShops.FirstOrDefaultAsync(s => s.Id == id);
        if (e == null) return NotFound();
        ViewBag.Cities = await CityOptionsAsync(m.CityId);
        m.Id = id;
        if (!ModelState.IsValid) return View("Form", m);
        e.Name = m.Name; e.CityId = m.CityId; e.Address = m.Address; e.Phone = m.Phone; e.IsActive = m.IsActive;
        await Db.SaveChangesAsync();
        Flash("Shop updated");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await Db.RetailShops.FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        var used = await Db.Employees.AnyAsync(e => e.RetailShopId == id) || await Db.ServiceOrders.AnyAsync(o => o.RetailShopId == id)
                   || await Db.Payments.AnyAsync(p => p.RetailShopId == id);
        if (used) { s.IsActive = false; Flash("Shop has history, so it was retired instead of deleted"); }
        else { Db.RetailShops.Remove(s); Flash("Shop deleted"); }
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = Roles.Admin)]
public class VendorsController : AppController
{
    public VendorsController(AppDbContext db, CurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index(string q)
    {
        var query = Db.Vendors.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(v => v.Name.Contains(q) || v.ContactPerson.Contains(q));
        ViewBag.Q = q;
        return View(await query.OrderBy(v => v.Name).ToListAsync());
    }

    public IActionResult Create() => View("Form", new Vendor());

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,ContactPerson,Phone,Email,Address,IsActive")] Vendor m)
    {
        if (!ModelState.IsValid) return View("Form", m);
        Db.Vendors.Add(m);
        await Db.SaveChangesAsync();
        Flash("Vendor added");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var m = await Db.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id);
        return m == null ? NotFound() : View("Form", m);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Name,ContactPerson,Phone,Email,Address,IsActive")] Vendor m)
    {
        var e = await Db.Vendors.FirstOrDefaultAsync(v => v.Id == id);
        if (e == null) return NotFound();
        m.Id = id;
        if (!ModelState.IsValid) return View("Form", m);
        e.Name = m.Name; e.ContactPerson = m.ContactPerson; e.Phone = m.Phone; e.Email = m.Email; e.Address = m.Address; e.IsActive = m.IsActive;
        await Db.SaveChangesAsync();
        Flash("Vendor updated");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var v = await Db.Vendors.FirstOrDefaultAsync(x => x.Id == id);
        if (v == null) return NotFound();
        var used = await Db.PurchaseOrders.AnyAsync(p => p.VendorId == id) || await Db.Products.AnyAsync(p => p.VendorId == id);
        if (used) { v.IsActive = false; Flash("Vendor has history, so it was retired instead of deleted"); }
        else { Db.Vendors.Remove(v); Flash("Vendor deleted"); }
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = Roles.Admin)]
public class CitiesController : AppController
{
    public CitiesController(AppDbContext db, CurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index() => View(await Db.Cities.AsNoTracking().OrderBy(c => c.Code).ToListAsync());

    public IActionResult Create() => View("Form", new City());

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,Code")] City m)
    {
        if (await Db.Cities.AnyAsync(c => c.Code == m.Code)) ModelState.AddModelError(nameof(m.Code), "Another city uses this code");
        if (!ModelState.IsValid) return View("Form", m);
        Db.Cities.Add(m);
        await Db.SaveChangesAsync();
        Flash("City added");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var m = await Db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (m == null) return NotFound();
        ViewBag.CodeLocked = await Db.Connections.AnyAsync(c => c.CityId == id);
        return View("Form", m);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Name,Code")] City m)
    {
        var e = await Db.Cities.FirstOrDefaultAsync(c => c.Id == id);
        if (e == null) return NotFound();
        var locked = await Db.Connections.AnyAsync(c => c.CityId == id);
        ViewBag.CodeLocked = locked;
        m.Id = id;
        if (locked) { m.Code = e.Code; ModelState.Remove(nameof(m.Code)); }
        if (await Db.Cities.AnyAsync(c => c.Code == m.Code && c.Id != id)) ModelState.AddModelError(nameof(m.Code), "Another city uses this code");
        if (!ModelState.IsValid) return View("Form", m);
        e.Name = m.Name; e.Code = m.Code;
        await Db.SaveChangesAsync();
        Flash("City updated");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await Db.Cities.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        var used = await Db.Customers.AnyAsync(x => x.CityId == id) || await Db.RetailShops.AnyAsync(x => x.CityId == id)
                   || await Db.ServiceOrders.AnyAsync(x => x.InstallCityId == id);
        if (used) Flash("This city is in use and cannot be deleted", "error");
        else { Db.Cities.Remove(c); await Db.SaveChangesAsync(); Flash("City deleted"); }
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = Roles.Admin)]
public class PlansController : AppController
{
    private const string Fields = "Name,ConnectionType,Category,SpeedKbps,Hours,ValidityMonths,Price,LocalRate,StdRate,MobileMsgRate,Description,IsActive";
    public PlansController(AppDbContext db, CurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index(string q, ConnectionType? type)
    {
        var query = Db.Plans.AsNoTracking().AsQueryable();
        if (type != null) query = query.Where(p => p.ConnectionType == type);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q) || p.Description.Contains(q));
        ViewBag.Q = q; ViewBag.Type = type;
        return View(await query.OrderBy(p => p.ConnectionType).ThenBy(p => p.Category).ThenBy(p => p.Price).ToListAsync());
    }

    public IActionResult Create() => View("Form", new Plan());

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Fields)] Plan m)
    {
        if (!ModelState.IsValid) return View("Form", m);
        Db.Plans.Add(m);
        await Db.SaveChangesAsync();
        Flash("Plan added");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var m = await Db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return m == null ? NotFound() : View("Form", m);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind(Fields)] Plan m)
    {
        var e = await Db.Plans.FirstOrDefaultAsync(p => p.Id == id);
        if (e == null) return NotFound();
        m.Id = id;
        if (!ModelState.IsValid) return View("Form", m);
        e.Name = m.Name; e.ConnectionType = m.ConnectionType; e.Category = m.Category; e.SpeedKbps = m.SpeedKbps;
        e.Hours = m.Hours; e.ValidityMonths = m.ValidityMonths; e.Price = m.Price; e.LocalRate = m.LocalRate;
        e.StdRate = m.StdRate; e.MobileMsgRate = m.MobileMsgRate; e.Description = m.Description; e.IsActive = m.IsActive;
        await Db.SaveChangesAsync();
        Flash("Plan updated. Existing connections keep their bills; the next bill uses the new price");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await Db.Plans.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var used = await Db.ServiceOrders.AnyAsync(o => o.PlanId == id || o.LandlinePlanId == id) || await Db.Connections.AnyAsync(c => c.PlanId == id);
        if (used) { p.IsActive = false; Flash("Plan is in use, so it was retired. New orders cannot choose it"); }
        else { Db.Plans.Remove(p); Flash("Plan deleted"); }
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Roles = Roles.Admin + "," + Roles.Technical)]
public class ProductsController : AppController
{
    private const string Fields = "Name,Category,Model,VendorId,ReplacementCharge,StockQty,ReorderLevel,Description,IsActive";
    public ProductsController(AppDbContext db, CurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index(string q, ProductCategory? category)
    {
        var query = Db.Products.AsNoTracking().Include(p => p.Vendor).AsQueryable();
        if (category != null) query = query.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q) || p.Model.Contains(q));
        var products = await query.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();

        var open = new[] { OrderStatus.Placed, OrderStatus.UnderFeasibilityCheck, OrderStatus.Feasible };
        var demand = await Db.ServiceOrders.Where(o => o.ProductId != null && open.Contains(o.Status))
            .GroupBy(o => o.ProductId.Value).Select(g => new { Id = g.Key, Qty = g.Sum(x => x.Quantity) }).ToListAsync();
        ViewBag.Demand = demand.ToDictionary(d => d.Id, d => d.Qty);
        ViewBag.Q = q; ViewBag.Category = category;
        return View(products);
    }

    public async Task<IActionResult> Create() { await VendorsAsync(null); return View("Form", new Product()); }

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Fields)] Product m)
    {
        await VendorsAsync(m.VendorId);
        if (!ModelState.IsValid) return View("Form", m);
        Db.Products.Add(m);
        await Db.SaveChangesAsync();
        Flash("Product added");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var m = await Db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (m == null) return NotFound();
        await VendorsAsync(m.VendorId);
        return View("Form", m);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind(Fields)] Product m)
    {
        var e = await Db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (e == null) return NotFound();
        await VendorsAsync(m.VendorId);
        m.Id = id;
        if (!ModelState.IsValid) return View("Form", m);
        e.Name = m.Name; e.Category = m.Category; e.Model = m.Model; e.VendorId = m.VendorId; e.ReplacementCharge = m.ReplacementCharge;
        e.StockQty = m.StockQty; e.ReorderLevel = m.ReorderLevel; e.Description = m.Description; e.IsActive = m.IsActive;
        await Db.SaveChangesAsync();
        Flash("Product updated");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await Db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        var used = await Db.ConnectionEquipment.AnyAsync(e => e.ProductId == id) || await Db.PurchaseOrderLines.AnyAsync(l => l.ProductId == id)
                   || await Db.ServiceOrders.AnyAsync(o => o.ProductId == id);
        if (used) { p.IsActive = false; Flash("Product has history, so it was retired instead of deleted"); }
        else { Db.Products.Remove(p); Flash("Product deleted"); }
        await Db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task VendorsAsync(int? selected)
    {
        var vendors = await Db.Vendors.AsNoTracking().Where(v => v.IsActive).OrderBy(v => v.Name).ToListAsync();
        ViewBag.Vendors = vendors.Select(v => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(v.Name, v.Id.ToString(), v.Id == selected)).ToList();
    }
}
