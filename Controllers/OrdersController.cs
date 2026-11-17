using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

/// <summary>
/// Orders for connections. Retail employees place walk-in orders, customers apply from the portal,
/// the technical team checks feasibility and connects, everyone with access can follow the status.
/// </summary>
public class OrdersController : AppController
{
    private readonly OrderService _orders;
    private readonly ConnectionService _connections;
    private readonly FileStore _files;

    public OrdersController(AppDbContext db, CurrentUser me, OrderService orders, ConnectionService connections, FileStore files)
        : base(db, me)
    {
        _orders = orders; _connections = connections; _files = files;
    }

    // ------------------------------------------------------------------ list and details
    public async Task<IActionResult> Index(OrderStatus? status, string q, int page = 1)
    {
        var query = Db.ServiceOrders.AsNoTracking().ForUser(Me)
            .Include(o => o.Customer).Include(o => o.Plan).Include(o => o.InstallCity).AsQueryable();
        if (status != null) query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var id = IdFormat.Normalize(q);
            query = query.Where(o => o.OrderNo.Contains(id) || o.Customer.FullName.Contains(q));
        }
        ViewBag.Status = status;
        ViewBag.Q = q;
        return View(await Paged<ServiceOrder>.CreateAsync(query.OrderByDescending(o => o.PlacedOn), page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await LoadOrderAsync(id);
        if (order == null) return NotFound();

        if (Me.Role == Roles.Technical)
        {
            ViewBag.Products = await Db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        }
        ViewBag.MaxKm = new Dictionary<FeasibilitySubject, decimal>
        {
            { FeasibilitySubject.Landline, await HttpContext.RequestServices.GetRequiredService<SettingsService>().GetDecimalAsync(SettingKeys.MaxKmLandline) },
            { FeasibilitySubject.Internet, await HttpContext.RequestServices.GetRequiredService<SettingsService>().GetDecimalAsync(
                order.ConnectionType == ConnectionType.Broadband ? SettingKeys.MaxKmBroadband : SettingKeys.MaxKmDialUp) }
        };
        return View(order);
    }

    private async Task<ServiceOrder> LoadOrderAsync(int id)
    {
        return await Db.ServiceOrders.AsNoTracking().ForUser(Me)
            .Include(o => o.Customer).Include(o => o.Plan).Include(o => o.LandlinePlan)
            .Include(o => o.Product).Include(o => o.InstallCity).Include(o => o.RetailShop)
            .Include(o => o.FeasibilityChecks).Include(o => o.Connections).Include(o => o.Documents)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    // ------------------------------------------------------------------ placing an order
    /// <summary>Retail employee: order for a walk-in customer chosen on the previous screen.</summary>
    [Authorize(Roles = Roles.Retail)]
    public async Task<IActionResult> New(int? customerId)
    {
        if (customerId == null) return RedirectToAction("Pick", "Customers");
        var customer = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId);
        if (customer == null) return NotFound();
        return View("Form", await BuildFormAsync(new OrderFormVm
        {
            CustomerId = customer.Id,
            InstallCityId = customer.CityId,
            InstallAddress = customer.Address
        }));
    }

    [HttpPost, Authorize(Roles = Roles.Retail)]
    public async Task<IActionResult> New(OrderFormVm vm)
    {
        if (Me.ShopId == null)
        {
            Flash("Your login is not linked to a retail shop. Ask the Admin to fix this", "error");
            return RedirectToAction("Index", "Dashboard");
        }
        return await SubmitAsync(vm, vm.CustomerId, OrderChannel.RetailShop, Me.ShopId);
    }

    /// <summary>Customer: apply from the portal for oneself.</summary>
    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Apply()
    {
        var customer = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == Me.CustomerId);
        if (customer == null) return NotFound();
        return View("Form", await BuildFormAsync(new OrderFormVm
        {
            CustomerId = customer.Id,
            InstallCityId = customer.CityId,
            InstallAddress = customer.Address
        }));
    }

    [HttpPost, Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Apply(OrderFormVm vm)
    {
        // The customer id comes from the sign-in, never from the posted form.
        return await SubmitAsync(vm, Me.CustomerId ?? -1, OrderChannel.Online, null);
    }

    private async Task<IActionResult> SubmitAsync(OrderFormVm vm, int customerId, OrderChannel channel, int? shopId)
    {
        if (!ModelState.IsValid) return View("Form", await BuildFormAsync(vm));

        var docs = new List<(DocumentKind, IFormFile)>();
        if (vm.AddressProof != null) docs.Add((DocumentKind.AddressProof, vm.AddressProof));
        if (vm.ApplicationForm != null) docs.Add((DocumentKind.ApplicationForm, vm.ApplicationForm));

        var result = await _orders.PlaceAsync(new PlaceOrderRequest
        {
            CustomerId = customerId,
            ConnectionType = vm.ConnectionType,
            PlanId = vm.PlanId ?? 0,
            ProductId = vm.ProductId,
            Quantity = vm.Quantity,
            HasExistingLandline = vm.HasExistingLandline,
            ExistingLandlineAccountId = vm.ExistingLandlineAccountId,
            LandlinePlanId = vm.LandlinePlanId,
            InstallCityId = vm.InstallCityId ?? 0,
            InstallAddress = vm.InstallAddress,
            Remarks = vm.Remarks,
            Channel = channel,
            RetailShopId = shopId,
            PlacedById = Me.Id,
            Documents = docs
        });

        if (!result.Ok)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e);
            vm.CustomerId = customerId;
            return View("Form", await BuildFormAsync(vm));
        }

        Flash("Order placed. Keep the order id to follow it");
        return RedirectToAction(nameof(Details), new { id = result.Order.Id });
    }

    private async Task<OrderFormVm> BuildFormAsync(OrderFormVm vm)
    {
        var customer = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == vm.CustomerId);
        vm.CustomerName = customer?.FullName;
        vm.Plans = await Db.Plans.AsNoTracking().Where(p => p.IsActive)
            .OrderBy(p => p.ConnectionType).ThenBy(p => p.Category).ThenBy(p => p.Price).ToListAsync();
        vm.Products = await Db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        vm.Cities = await Db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        vm.Schemes = await Db.DiscountSchemes.AsNoTracking().OrderBy(s => s.MinConnections).ToListAsync();
        vm.Deposits = await Db.DepositRates.AsNoTracking().ToListAsync();
        return vm;
    }

    // ------------------------------------------------------------------ technical team actions
    [HttpPost, Authorize(Roles = Roles.Technical)]
    public async Task<IActionResult> StartCheck(int id)
    {
        var err = await _orders.StartCheckAsync(id);
        if (err != null) Flash(err, "error"); else Flash("Order moved to feasibility check");
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Technical)]
    public async Task<IActionResult> Feasibility(FeasibilityVm vm)
    {
        if (!ModelState.IsValid) { Flash("Enter the distance in kilometres", "error"); return RedirectToAction(nameof(Details), new { id = vm.OrderId }); }
        var err = await _orders.RecordFeasibilityAsync(vm.OrderId, vm.Subject, vm.DistanceKm, vm.ServerAvailable, vm.Remarks, Me.Id);
        if (err != null) Flash(err, "error"); else Flash(vm.Subject.Label() + " check recorded");
        return RedirectToAction(nameof(Details), new { id = vm.OrderId });
    }

    [HttpPost, Authorize(Roles = Roles.Technical)]
    public async Task<IActionResult> Provision(ProvisionVm vm)
    {
        var (created, err) = await _connections.ProvisionAsync(vm.OrderId, vm.ProductId, vm.LandlineProductId, vm.Serials, Me.Id);
        if (err != null)
        {
            Flash(err, "error");
            return RedirectToAction(nameof(Details), new { id = vm.OrderId });
        }
        Flash(created.Count == 1 ? "Connection created" : created.Count + " connections created");
        return RedirectToAction(nameof(Details), new { id = vm.OrderId });
    }

    // ------------------------------------------------------------------ cancel and documents
    [HttpPost, Authorize(Roles = Roles.Retail + "," + Roles.Customer + "," + Roles.Admin)]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        // Retail and customers can only touch orders inside their own scope.
        var visible = await Db.ServiceOrders.AsNoTracking().ForUser(Me).AnyAsync(o => o.Id == id);
        if (!visible) return NotFound();
        var err = await _orders.CancelAsync(id, reason);
        if (err != null) Flash(err, "error"); else Flash("Order cancelled");
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Document(int id)
    {
        var doc = await Db.OrderDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null || doc.ServiceOrderId == null) return NotFound();
        var allowed = await Db.ServiceOrders.AsNoTracking().ForUser(Me).AnyAsync(o => o.Id == doc.ServiceOrderId);
        if (!allowed) return NotFound();
        var stream = _files.Open(doc.StoredPath);
        if (stream == null) return NotFound();
        return File(stream, FileStore.ContentType(doc.OriginalName), doc.OriginalName);
    }
}
