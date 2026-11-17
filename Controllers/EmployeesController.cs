using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class EmployeesController : AppController
{
    private readonly SequenceService _seq;
    public EmployeesController(AppDbContext db, CurrentUser me, SequenceService seq) : base(db, me) { _seq = seq; }

    public async Task<IActionResult> Index(string role, string q, int page = 1)
    {
        var query = Db.Employees.AsNoTracking().Include(e => e.User).Include(e => e.RetailShop).AsQueryable();
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(e => e.User.Role == role);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(e => e.User.FullName.Contains(q) || e.User.Username.Contains(q) || e.EmployeeNo.Contains(q));
        ViewBag.Role = role; ViewBag.Q = q;
        return View(await Paged<Employee>.CreateAsync(query.OrderBy(e => e.User.FullName), page));
    }

    public async Task<IActionResult> Create() => View("Form", new EmployeeVm { Shops = await ShopsAsync(null) });

    [HttpPost]
    public async Task<IActionResult> Create(EmployeeVm vm)
    {
        vm.Shops = await ShopsAsync(vm.RetailShopId);
        vm.Username = vm.Username?.Trim();
        Validate(vm, creating: true);
        if (await Db.Users.AnyAsync(u => u.Username == vm.Username)) ModelState.AddModelError(nameof(vm.Username), "That username is taken");
        if (!ModelState.IsValid) return View("Form", vm);

        var user = new AppUser
        {
            Username = vm.Username, PasswordHash = PasswordTool.Hash(vm.Password), Role = vm.Role,
            FullName = vm.FullName.Trim(), Email = vm.Email.Trim(), Phone = NormalizePhone(vm.Phone), IsActive = vm.IsActive
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        Db.Employees.Add(new Employee
        {
            UserId = user.Id, EmployeeNo = await _seq.EmployeeNoAsync(), Address = vm.Address?.Trim(),
            HireDate = vm.HireDate, RetailShopId = vm.Role == Roles.Retail ? vm.RetailShopId : null
        });
        await Db.SaveChangesAsync();
        Flash("Employee added");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var e = await Db.Employees.AsNoTracking().Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        return View("Form", new EmployeeVm
        {
            Id = e.Id, FullName = e.User.FullName, Username = e.User.Username, Email = e.User.Email, Phone = e.User.Phone,
            Address = e.Address, Role = e.User.Role, RetailShopId = e.RetailShopId, HireDate = e.HireDate,
            IsActive = e.User.IsActive, Shops = await ShopsAsync(e.RetailShopId)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, EmployeeVm vm)
    {
        vm.Id = id;
        vm.Shops = await ShopsAsync(vm.RetailShopId);
        vm.Username = vm.Username?.Trim();
        Validate(vm, creating: false);
        var e = await Db.Employees.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        if (await Db.Users.AnyAsync(u => u.Username == vm.Username && u.Id != e.UserId)) ModelState.AddModelError(nameof(vm.Username), "That username is taken");
        if (e.UserId == Me.Id && (!vm.IsActive || vm.Role != Roles.Admin))
            ModelState.AddModelError("", "You cannot deactivate yourself or change your own role");
        if (!ModelState.IsValid) return View("Form", vm);

        e.User.FullName = vm.FullName.Trim(); e.User.Username = vm.Username; e.User.Email = vm.Email.Trim();
        e.User.Phone = NormalizePhone(vm.Phone); e.User.Role = vm.Role; e.User.IsActive = vm.IsActive;
        if (!string.IsNullOrEmpty(vm.Password)) e.User.PasswordHash = PasswordTool.Hash(vm.Password);
        e.Address = vm.Address?.Trim(); e.HireDate = vm.HireDate;
        e.RetailShopId = vm.Role == Roles.Retail ? vm.RetailShopId : null;
        await Db.SaveChangesAsync();
        Flash("Employee updated");
        return RedirectToAction(nameof(Index));
    }

    private void Validate(EmployeeVm vm, bool creating)
    {
        if (!Roles.EmployeeRoles.Contains(vm.Role)) ModelState.AddModelError(nameof(vm.Role), "Choose a role");
        if (vm.Role == Roles.Retail && vm.RetailShopId == null) ModelState.AddModelError(nameof(vm.RetailShopId), "Retail employees belong to a shop");
        if (creating || !string.IsNullOrEmpty(vm.Password))
        {
            var pw = PasswordTool.Check(vm.Password);
            if (pw != null) ModelState.AddModelError(nameof(vm.Password), pw);
        }
    }

    private async Task<List<SelectListItem>> ShopsAsync(int? selected)
    {
        var shops = await Db.RetailShops.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        return shops.Select(s => new SelectListItem(s.Name, s.Id.ToString(), s.Id == selected)).ToList();
    }
}
