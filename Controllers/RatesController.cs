using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

/// <summary>Security deposits, bulk discount tiers, service tax and feasibility limits.</summary>
[Authorize(Roles = Roles.Admin)]
public class RatesController : AppController
{
    private readonly SettingsService _settings;
    public RatesController(AppDbContext db, CurrentUser me, SettingsService settings) : base(db, me) { _settings = settings; }

    public async Task<IActionResult> Index()
    {
        ViewBag.Settings = new SettingsVm
        {
            TaxPercent = await _settings.GetDecimalAsync(SettingKeys.TaxPercent),
            DueDays = await _settings.GetIntAsync(SettingKeys.DueDays),
            MaxKmLandline = await _settings.GetDecimalAsync(SettingKeys.MaxKmLandline),
            MaxKmBroadband = await _settings.GetDecimalAsync(SettingKeys.MaxKmBroadband),
            MaxKmDialUp = await _settings.GetDecimalAsync(SettingKeys.MaxKmDialUp)
        };
        var deposits = await Db.DepositRates.AsNoTracking().ToListAsync();
        decimal D(ConnectionType t) => deposits.FirstOrDefault(d => d.ConnectionType == t)?.Amount ?? 0;
        ViewBag.Deposits = new DepositsVm { DialUp = D(ConnectionType.DialUp), Broadband = D(ConnectionType.Broadband), Telephone = D(ConnectionType.Telephone) };
        return View(await Db.DiscountSchemes.AsNoTracking().OrderBy(s => s.MinConnections).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> SaveSettings(SettingsVm vm)
    {
        if (!ModelState.IsValid) { Flash("Check the values: " + string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)), "error"); return RedirectToAction(nameof(Index)); }
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        await _settings.SetAsync(SettingKeys.TaxPercent, vm.TaxPercent.ToString(inv));
        await _settings.SetAsync(SettingKeys.DueDays, vm.DueDays.ToString(inv));
        await _settings.SetAsync(SettingKeys.MaxKmLandline, vm.MaxKmLandline.ToString(inv));
        await _settings.SetAsync(SettingKeys.MaxKmBroadband, vm.MaxKmBroadband.ToString(inv));
        await _settings.SetAsync(SettingKeys.MaxKmDialUp, vm.MaxKmDialUp.ToString(inv));
        Flash("Settings saved");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> SaveDeposits(DepositsVm vm)
    {
        if (!ModelState.IsValid) { Flash("Deposits must be between 0 and 100000", "error"); return RedirectToAction(nameof(Index)); }
        await SetDepositAsync(ConnectionType.DialUp, vm.DialUp);
        await SetDepositAsync(ConnectionType.Broadband, vm.Broadband);
        await SetDepositAsync(ConnectionType.Telephone, vm.Telephone);
        await Db.SaveChangesAsync();
        Flash("Security deposits saved. They apply to connections made from now on");
        return RedirectToAction(nameof(Index));
    }

    private async Task SetDepositAsync(ConnectionType t, decimal amount)
    {
        var row = await Db.DepositRates.FirstOrDefaultAsync(d => d.ConnectionType == t);
        if (row == null) Db.DepositRates.Add(new SecurityDepositRate { ConnectionType = t, Amount = amount });
        else row.Amount = amount;
    }

    [HttpPost]
    public async Task<IActionResult> SaveScheme(int id, [Bind("Name,MinConnections,MaxConnections,Percent")] DiscountScheme m)
    {
        if (m.MaxConnections != null && m.MaxConnections < m.MinConnections)
            ModelState.AddModelError(nameof(m.MaxConnections), "Upper limit cannot be below the lower limit");
        // Tiers must not overlap, or one order could match two discounts.
        var others = await Db.DiscountSchemes.Where(s => s.Id != id).ToListAsync();
        var max = m.MaxConnections ?? int.MaxValue;
        if (others.Any(s => m.MinConnections <= (s.MaxConnections ?? int.MaxValue) && s.MinConnections <= max))
            ModelState.AddModelError("", "That range overlaps another tier");
        if (!ModelState.IsValid)
        {
            Flash(string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)), "error");
            return RedirectToAction(nameof(Index));
        }
        if (id == 0) Db.DiscountSchemes.Add(m);
        else
        {
            var e = await Db.DiscountSchemes.FirstOrDefaultAsync(s => s.Id == id);
            if (e == null) return NotFound();
            e.Name = m.Name; e.MinConnections = m.MinConnections; e.MaxConnections = m.MaxConnections; e.Percent = m.Percent;
        }
        await Db.SaveChangesAsync();
        Flash("Discount tier saved");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteScheme(int id)
    {
        var e = await Db.DiscountSchemes.FirstOrDefaultAsync(s => s.Id == id);
        if (e != null) { Db.DiscountSchemes.Remove(e); await Db.SaveChangesAsync(); Flash("Discount tier deleted"); }
        return RedirectToAction(nameof(Index));
    }
}
