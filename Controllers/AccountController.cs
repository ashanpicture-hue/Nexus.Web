using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

public class AccountController : AppController
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(10);

    public AccountController(AppDbContext db, CurrentUser me) : base(db, me) { }

    [AllowAnonymous]
    public IActionResult Login(string returnUrl)
    {
        if (Me.IsAuthenticated) return RedirectToAction("Index", "Dashboard");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var username = vm.Username.Trim();
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == username);

        const string generic = "Username or password is not right";
        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError("", generic);
            return View(vm);
        }
        if (user.LockedUntil != null && user.LockedUntil > DateTime.Now)
        {
            ModelState.AddModelError("", "Too many attempts. Try again in a few minutes");
            return View(vm);
        }
        if (!PasswordTool.Verify(vm.Password, user.PasswordHash))
        {
            user.FailedLogins++;
            if (user.FailedLogins >= MaxFailures)
            {
                user.LockedUntil = DateTime.Now.Add(LockFor);
                user.FailedLogins = 0;
            }
            await Db.SaveChangesAsync();
            ModelState.AddModelError("", generic);
            return View(vm);
        }

        user.FailedLogins = 0;
        user.LockedUntil = null;
        await Db.SaveChangesAsync();
        await SignInAsync(user);

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return LocalRedirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Dashboard");
    }

    private async Task SignInAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.Role == Roles.Customer)
        {
            var cust = await Db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == user.Id);
            if (cust != null) claims.Add(new Claim("customer", cust.Id.ToString()));
        }
        else
        {
            var emp = await Db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.UserId == user.Id);
            if (emp?.RetailShopId != null) claims.Add(new Claim("shop", emp.RetailShopId.Value.ToString()));
        }
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public async Task<IActionResult> Register()
    {
        if (Me.IsAuthenticated) return RedirectToAction("Index", "Dashboard");
        return View(new RegisterVm { Cities = await CityOptionsAsync() });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        vm.Cities = await CityOptionsAsync(vm.CityId);
        vm.Username = vm.Username?.Trim();

        if (ModelState.IsValid)
        {
            var pwError = PasswordTool.Check(vm.Password);
            if (pwError != null) ModelState.AddModelError(nameof(vm.Password), pwError);
            if (await Db.Users.AnyAsync(u => u.Username == vm.Username))
                ModelState.AddModelError(nameof(vm.Username), "That username is taken");
            if (vm.Kind == CustomerKind.Corporate && string.IsNullOrWhiteSpace(vm.CompanyName))
                ModelState.AddModelError(nameof(vm.CompanyName), "Enter the company name");
        }
        if (!ModelState.IsValid) return View(vm);

        var user = new AppUser
        {
            Username = vm.Username,
            PasswordHash = PasswordTool.Hash(vm.Password),
            Role = Roles.Customer,
            FullName = vm.FullName.Trim(),
            Email = vm.Email.Trim(),
            Phone = NormalizePhone(vm.Phone)
        };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        Db.Customers.Add(new Customer
        {
            UserId = user.Id,
            FullName = user.FullName,
            Kind = vm.Kind,
            CompanyName = vm.Kind == CustomerKind.Corporate ? vm.CompanyName?.Trim() : null,
            Email = user.Email,
            Phone = user.Phone,
            Address = vm.Address.Trim(),
            CityId = vm.CityId.Value
        });
        await Db.SaveChangesAsync();

        await SignInAsync(user);
        Flash("Your account is ready. You can now apply for a connection");
        return RedirectToAction("Index", "Dashboard");
    }

    public IActionResult Password() => View(new ChangePasswordVm());

    [HttpPost]
    public async Task<IActionResult> Password(ChangePasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Id == Me.Id);
        if (user == null || !PasswordTool.Verify(vm.Current, user.PasswordHash))
            ModelState.AddModelError(nameof(vm.Current), "Current password is not right");
        var pwError = PasswordTool.Check(vm.New);
        if (pwError != null) ModelState.AddModelError(nameof(vm.New), pwError);
        if (!ModelState.IsValid) return View(vm);

        user.PasswordHash = PasswordTool.Hash(vm.New);
        await Db.SaveChangesAsync();
        Flash("Password changed");
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
