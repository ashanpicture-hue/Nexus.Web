using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

/// <summary>
/// Advanced search of orders or connections by id, name, connection type, date range and contact number.
/// Staff search everything their role allows; customers search only their own records.
/// </summary>
public class SearchController : AppController
{
    public SearchController(AppDbContext db, CurrentUser me) : base(db, me) { }

    public async Task<IActionResult> Index(SearchVm vm)
    {
        // The form has no submit flag of its own: any filter counts as a search.
        vm.Submitted = !string.IsNullOrWhiteSpace(vm.Id) || !string.IsNullOrWhiteSpace(vm.Name) ||
                       !string.IsNullOrWhiteSpace(vm.Phone) || vm.Type != null || vm.From != null || vm.To != null;
        if (!vm.Submitted) return View(vm);

        var id = IdFormat.Normalize(vm.Id);
        var phone = NormalizePhone(vm.Phone);
        var to = vm.To?.Date.AddDays(1);   // include the whole last day

        if (vm.Target == "connections")
        {
            var q = Db.Connections.AsNoTracking().ForUser(Me)
                .Include(c => c.Customer).Include(c => c.Plan).Include(c => c.City).AsQueryable();
            if (id != "") q = q.Where(c => c.AccountId.Contains(id));
            if (!string.IsNullOrWhiteSpace(vm.Name)) q = q.Where(c => c.Customer.FullName.Contains(vm.Name) || c.Customer.CompanyName.Contains(vm.Name));
            if (phone != "") q = q.Where(c => c.Customer.Phone.Contains(phone));
            if (vm.Type != null) q = q.Where(c => c.ConnectionType == vm.Type);
            if (vm.From != null) q = q.Where(c => c.ActivatedOn >= vm.From.Value.Date);
            if (to != null) q = q.Where(c => c.ActivatedOn < to);
            vm.Connections = await q.OrderByDescending(c => c.ActivatedOn).Take(100).ToListAsync();
        }
        else
        {
            vm.Target = "orders";
            var q = Db.ServiceOrders.AsNoTracking().ForUser(Me)
                .Include(o => o.Customer).Include(o => o.Plan).Include(o => o.InstallCity).AsQueryable();
            if (id != "") q = q.Where(o => o.OrderNo.Contains(id));
            if (!string.IsNullOrWhiteSpace(vm.Name)) q = q.Where(o => o.Customer.FullName.Contains(vm.Name) || o.Customer.CompanyName.Contains(vm.Name));
            if (phone != "") q = q.Where(o => o.Customer.Phone.Contains(phone));
            if (vm.Type != null) q = q.Where(o => o.ConnectionType == vm.Type);
            if (vm.From != null) q = q.Where(o => o.PlacedOn >= vm.From.Value.Date);
            if (to != null) q = q.Where(o => o.PlacedOn < to);
            vm.Orders = await q.OrderByDescending(o => o.PlacedOn).Take(100).ToListAsync();
        }
        return View(vm);
    }
}
