using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;
using Nexus.Web.Services;
using Nexus.Web.ViewModels;

namespace Nexus.Web.Controllers;

public class FeedbackController : AppController
{
    public FeedbackController(AppDbContext db, CurrentUser me) : base(db, me) { }

    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = Db.Feedbacks.AsNoTracking().Include(f => f.Customer).Include(f => f.ServiceOrder).OrderByDescending(f => f.CreatedOn);
        var paged = await Paged<Feedback>.CreateAsync(query, page);
        var all = await Db.Feedbacks.Select(f => f.Rating).ToListAsync();
        ViewBag.Average = all.Count == 0 ? 0 : all.Average();
        ViewBag.Count = all.Count;
        return View(paged);
    }

    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Create(int? orderId)
    {
        var vm = await FormAsync(new FeedbackVm { ServiceOrderId = orderId });
        return View(vm);
    }

    [HttpPost, Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Create(FeedbackVm vm)
    {
        if (vm.ServiceOrderId != null &&
            !await Db.ServiceOrders.AnyAsync(o => o.Id == vm.ServiceOrderId && o.CustomerId == Me.CustomerId))
            ModelState.AddModelError(nameof(vm.ServiceOrderId), "Choose one of your own orders");
        if (!ModelState.IsValid) return View(await FormAsync(vm));

        Db.Feedbacks.Add(new Feedback
        {
            CustomerId = Me.CustomerId.Value,
            ServiceOrderId = vm.ServiceOrderId,
            Rating = vm.Rating,
            Comments = vm.Comments.Trim()
        });
        await Db.SaveChangesAsync();
        Flash("Thank you. Your feedback is with the manager");
        return RedirectToAction("Index", "Dashboard");
    }

    private async Task<FeedbackVm> FormAsync(FeedbackVm vm)
    {
        vm.Orders = await Db.ServiceOrders.AsNoTracking().Where(o => o.CustomerId == Me.CustomerId)
            .OrderByDescending(o => o.PlacedOn).Take(20).ToListAsync();
        return vm;
    }
}
