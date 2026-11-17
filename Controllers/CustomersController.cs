using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

/// <summary>Customer records. Staff look them up; retail and admin register and edit them; customers edit their own contact details.</summary>
public class CustomersController : AppController
{
    public CustomersController(AppDbContext db, CurrentUser me) : base(db, me) { }

    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Index(string q, int page = 1)
    {
        var query = Db.Customers.AsNoTracking().Include(c => c.City).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var phone = NormalizePhone(q);
            query = query.Where(c => c.FullName.Contains(q) || c.CompanyName.Contains(q) ||
                                     (phone != "" && c.Phone.Contains(phone)) || c.Email.Contains(q));
        }
        ViewBag.Q = q;
        return View(await Paged<Customer>.CreateAsync(query.OrderBy(c => c.FullName), page));
    }

    /// <summary>First step of a walk-in order: find the customer by phone or name, or add a new one.</summary>
    [Authorize(Roles = Roles.Retail)]
    public async Task<IActionResult> Pick(string q)
    {
        ViewBag.Q = q;
        if (string.IsNullOrWhiteSpace(q)) return View(new List<Customer>());
        var phone = NormalizePhone(q);
        var found = await Db.Customers.AsNoTracking().Include(c => c.City)
            .Where(c => c.FullName.Contains(q) || c.CompanyName.Contains(q) || (phone != "" && c.Phone.Contains(phone)))
            .OrderBy(c => c.FullName).Take(15).ToListAsync();
        return View(found);
    }

    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Details(int id)
    {
        var c = await Db.Customers.AsNoTracking().Include(x => x.City).Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        // Retail staff see only the orders and connections from their own shop.
        ViewBag.Orders = await Db.ServiceOrders.AsNoTracking().ForUser(Me).Include(o => o.Plan)
            .Where(o => o.CustomerId == id).OrderByDescending(o => o.PlacedOn).ToListAsync();
        ViewBag.Connections = await Db.Connections.AsNoTracking().ForUser(Me).Include(x => x.Plan)
            .Where(x => x.CustomerId == id).OrderBy(x => x.AccountId).ToListAsync();
        return View(c);
    }

    [Authorize(Roles = Roles.Retail + "," + Roles.Admin)]
    public async Task<IActionResult> Create() => View("Form", new CustomerVm { Cities = await CityOptionsAsync() });

    [HttpPost, Authorize(Roles = Roles.Retail + "," + Roles.Admin)]
    public async Task<IActionResult> Create(CustomerVm vm)
    {
        vm.Cities = await CityOptionsAsync(vm.CityId);
        await ValidateAsync(vm);
        if (vm.CreateLogin)
        {
            if (string.IsNullOrWhiteSpace(vm.Username)) ModelState.AddModelError(nameof(vm.Username), "Enter a username");
            else if (await Db.Users.AnyAsync(u => u.Username == vm.Username.Trim())) ModelState.AddModelError(nameof(vm.Username), "That username is taken");
            var pw = PasswordTool.Check(vm.Password);
            if (pw != null) ModelState.AddModelError(nameof(vm.Password), pw);
        }
        if (!ModelState.IsValid) return View("Form", vm);

        int? userId = null;
        if (vm.CreateLogin)
        {
            var user = new AppUser
            {
                Username = vm.Username.Trim(),
                PasswordHash = PasswordTool.Hash(vm.Password),
                Role = Roles.Customer,
                FullName = vm.FullName.Trim(),
                Email = vm.Email?.Trim(),
                Phone = NormalizePhone(vm.Phone)
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            userId = user.Id;
        }

        var customer = new Customer { UserId = userId };
        Apply(customer, vm);
        Db.Customers.Add(customer);
        await Db.SaveChangesAsync();

        Flash("Customer saved");
        // Retail staff go straight on to the order.
        if (Me.Role == Roles.Retail) return RedirectToAction("New", "Orders", new { customerId = customer.Id });
        return RedirectToAction(nameof(Details), new { id = customer.Id });
    }

    [Authorize(Roles = Roles.Retail + "," + Roles.Admin)]
    public async Task<IActionResult> Edit(int id)
    {
        var c = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        return View("Form", ToVm(c, await CityOptionsAsync(c.CityId)));
    }

    [HttpPost, Authorize(Roles = Roles.Retail + "," + Roles.Admin)]
    public async Task<IActionResult> Edit(int id, CustomerVm vm)
    {
        vm.Id = id;
        vm.Cities = await CityOptionsAsync(vm.CityId);
        await ValidateAsync(vm);
        if (!ModelState.IsValid) return View("Form", vm);
        var c = await Db.Customers.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        Apply(c, vm);
        await Db.SaveChangesAsync();
        Flash("Customer updated");
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>A customer's own contact details.</summary>
    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Profile()
    {
        var c = await Db.Customers.AsNoTracking().Include(x => x.City).FirstOrDefaultAsync(x => x.Id == Me.CustomerId);
        if (c == null) return NotFound();
        return View(c);
    }

    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> EditProfile()
    {
        var c = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Me.CustomerId);
        if (c == null) return NotFound();
        return View(ToVm(c, await CityOptionsAsync(c.CityId)));
    }

    [HttpPost, Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> EditProfile(CustomerVm vm)
    {
        vm.Cities = await CityOptionsAsync(vm.CityId);
        await ValidateAsync(vm);
        if (!ModelState.IsValid) return View(vm);
        var c = await Db.Customers.FirstOrDefaultAsync(x => x.Id == Me.CustomerId);
        if (c == null) return NotFound();
        Apply(c, vm);
        await Db.SaveChangesAsync();
        Flash("Contact details saved");
        return RedirectToAction(nameof(Profile));
    }

    private Task ValidateAsync(CustomerVm vm)
    {
        if (vm.Kind == CustomerKind.Corporate && string.IsNullOrWhiteSpace(vm.CompanyName))
            ModelState.AddModelError(nameof(vm.CompanyName), "Enter the company name");
        if (NormalizePhone(vm.Phone).Length < 7)
            ModelState.AddModelError(nameof(vm.Phone), "Enter a phone number with at least 7 digits");
        // Customers cannot change their account type or ID proof; keep the checks cheap and shared.
        return Task.CompletedTask;
    }

    private static void Apply(Customer c, CustomerVm vm)
    {
        c.FullName = vm.FullName.Trim();
        c.Kind = vm.Kind;
        c.CompanyName = vm.Kind == CustomerKind.Corporate ? vm.CompanyName?.Trim() : null;
        c.Email = vm.Email?.Trim();
        c.Phone = NormalizePhone(vm.Phone);
        c.Address = vm.Address.Trim();
        c.CityId = vm.CityId.Value;
        c.IdProofType = vm.IdProofType?.Trim();
        c.IdProofNumber = vm.IdProofNumber?.Trim();
    }

    private static CustomerVm ToVm(Customer c, List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> cities) => new()
    {
        Id = c.Id, FullName = c.FullName, Kind = c.Kind, CompanyName = c.CompanyName, Email = c.Email,
        Phone = c.Phone, Address = c.Address, CityId = c.CityId, IdProofType = c.IdProofType,
        IdProofNumber = c.IdProofNumber, Cities = cities
    };
}
