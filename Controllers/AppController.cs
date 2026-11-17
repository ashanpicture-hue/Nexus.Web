using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Services;

namespace Nexus.Web.Controllers;

/// <summary>Shared plumbing for controllers: database, signed-in user and flash messages.</summary>
public abstract class AppController : Controller
{
    protected readonly AppDbContext Db;
    protected readonly CurrentUser Me;

    protected AppController(AppDbContext db, CurrentUser me)
    {
        Db = db;
        Me = me;
    }

    /// <summary>One-time message shown at the top of the next page. kind is ok or error.</summary>
    protected void Flash(string message, string kind = "ok")
    {
        TempData["flash"] = message;
        TempData["flashKind"] = kind;
    }

    protected async Task<List<SelectListItem>> CityOptionsAsync(int? selected = null)
    {
        var cities = await Db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        return cities.Select(c => new SelectListItem($"{c.Name} ({c.Code})", c.Id.ToString(), c.Id == selected)).ToList();
    }

    /// <summary>Keeps digits and a leading plus, so phone numbers can be compared however they were typed.</summary>
    public static string NormalizePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var trimmed = raw.Trim();
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        return trimmed.StartsWith("+") ? "+" + digits : digits;
    }
}
