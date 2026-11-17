using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;

namespace Nexus.Web.Controllers;

/// <summary>Public pages: home with the rate card, the plan catalogue and the error page.</summary>
[AllowAnonymous]
public class HomeController : AppController
{
    public HomeController(AppDbContext db, CurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index()
    {
        if (Me.IsAuthenticated) return RedirectToAction("Index", "Dashboard");

        // The rate card on the home page is read straight from the plan table, so it is never out of date.
        var card = await Db.Plans.AsNoTracking()
            .Where(p => p.IsActive && p.ValidityMonths == 1 && p.Category != PlanCategory.Hourly)
            .OrderBy(p => p.ConnectionType).ThenBy(p => p.Price).ToListAsync();
        ViewBag.Schemes = await Db.DiscountSchemes.AsNoTracking().OrderBy(s => s.MinConnections).ToListAsync();
        ViewBag.Deposits = await Db.DepositRates.AsNoTracking().OrderBy(d => d.ConnectionType).ToListAsync();
        return View(card);
    }

    /// <summary>Plan catalogue, searchable by name.</summary>
    public async Task<IActionResult> Plans(string q)
    {
        var query = Db.Plans.AsNoTracking().Where(p => p.IsActive);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q) || p.Description.Contains(q));
        ViewBag.Q = q;
        ViewBag.Deposits = await Db.DepositRates.AsNoTracking().ToListAsync();
        ViewBag.TaxPercent = await HttpContext.RequestServices.GetRequiredService<SettingsService>().GetDecimalAsync(SettingKeys.TaxPercent);
        return View(await query.OrderBy(p => p.ConnectionType).ThenBy(p => p.Category).ThenBy(p => p.Price).ToListAsync());
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Error() => View();
}
