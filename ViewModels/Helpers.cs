using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Models;
using Nexus.Web.Services;

namespace Nexus.Web.ViewModels;

/// <summary>Number and date formats used on every screen, so figures always look the same.</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string Money(decimal v) => (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("N2", Inv);
    public static string Rate(decimal? v) => v == null ? "none" : "$" + v.Value.ToString("0.00##", Inv) + " / min";
    public static string Date(DateTime? d) => d == null ? "none" : d.Value.ToString("d MMM yyyy", Inv);
    public static string DateTimeText(DateTime d) => d.ToString("d MMM yyyy, HH:mm", Inv);
    public static string Percent(decimal v) => v.ToString("0.##", Inv) + "%";
    public static string Num(decimal v) => v.ToString("0.##", Inv);
}

public static class EnumExt
{
    /// <summary>Reads the [Display(Name)] text of an enum value.</summary>
    public static string Label(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        var attr = member?.GetCustomAttribute<DisplayAttribute>();
        return attr?.Name ?? value.ToString();
    }

    public static List<SelectListItem> Options<T>(T? selected = null) where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new SelectListItem(v.Label(), Convert.ToInt32(v).ToString(), selected.HasValue && selected.Value.Equals(v))).ToList();
}

/// <summary>Small HTML building blocks shared by the views: icons, status dots and id tags.</summary>
public static class Ui
{
    public static IHtmlContent Icon(string name, int size = 16)
    {
        if (!IconData.Paths.TryGetValue(name, out var inner)) return HtmlString.Empty;
        return new HtmlString($"<svg class=\"ic\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">{inner}</svg>");
    }

    /// <summary>The signature element: an id set like a cable label, chunked so it can be read aloud and copied.</summary>
    public static IHtmlContent Id(string id)
    {
        if (string.IsNullOrEmpty(id)) return HtmlString.Empty;
        var clean = IdFormat.Normalize(id);
        return new HtmlString($"<span class=\"idtag\" data-id=\"{WebUtility.HtmlEncode(clean)}\">{WebUtility.HtmlEncode(IdFormat.Chunk(clean))}</span>");
    }

    private static IHtmlContent Dot(string tone, string text) =>
        new HtmlString($"<span class=\"status\"><i class=\"dot dot-{tone}\"></i>{WebUtility.HtmlEncode(text)}</span>");

    public static IHtmlContent Status(OrderStatus s) => Dot(s switch
    {
        OrderStatus.Placed => "grey",
        OrderStatus.UnderFeasibilityCheck => "blue",
        OrderStatus.Feasible => "teal",
        OrderStatus.NotFeasible => "red",
        OrderStatus.ConnectionProvided => "green",
        _ => "muted"
    }, s.Label());

    public static IHtmlContent Status(ConnectionStatus s) => Dot(s switch
    {
        ConnectionStatus.Active => "green",
        ConnectionStatus.TemporarilyInactive => "amber",
        _ => "red"
    }, s.Label());

    public static IHtmlContent Status(BillStatus s) => Dot(s switch
    {
        BillStatus.Paid => "green",
        BillStatus.PartiallyPaid => "amber",
        _ => "red"
    }, s.Label());

    public static IHtmlContent Status(PurchaseStatus s) => Dot(s switch
    {
        PurchaseStatus.Received => "green",
        PurchaseStatus.Cancelled => "muted",
        _ => "blue"
    }, s.Label());

    public static IHtmlContent Active(bool active) => Dot(active ? "green" : "muted", active ? "Active" : "Retired");
}

/// <summary>One page of a list.</summary>
public abstract class PagedBase
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int Total { get; set; }
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public class Paged<T> : PagedBase
{
    public List<T> Items { get; set; } = new();

    public static async Task<Paged<T>> CreateAsync(IQueryable<T> query, int page, int pageSize = 20)
    {
        page = Math.Max(1, page);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new Paged<T> { Items = items, Page = page, PageSize = pageSize, Total = total };
    }
}
