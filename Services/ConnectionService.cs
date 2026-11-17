using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;

namespace Nexus.Web.Services;

/// <summary>Creating connections from feasible orders and managing them afterwards.</summary>
public class ConnectionService
{
    private readonly AppDbContext _db;
    private readonly SequenceService _seq;

    public ConnectionService(AppDbContext db, SequenceService seq) { _db = db; _seq = seq; }

    private void Log(Connection c, string evt, string detail, int? userId)
    {
        c.Events.Add(new ConnectionEvent { Event = evt, Detail = detail, ByUserId = userId, At = DateTime.Now });
    }

    /// <summary>
    /// Turns a feasible order into live connections. An order for N connections creates N accounts.
    /// A dial-up order without an existing landline also creates a telephone connection for each,
    /// and links the dial-up account to it. Equipment is issued from stock and stock is reduced.
    /// </summary>
    public async Task<(List<Connection> Created, string Error)> ProvisionAsync(
        int orderId, int? productId, int? landlineProductId, string serialNumbers, int userId)
    {
        var order = await _db.ServiceOrders
            .Include(o => o.InstallCity).Include(o => o.Plan).Include(o => o.LandlinePlan)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return (null, "Order not found");
        if (order.Status != OrderStatus.Feasible) return (null, "Only a feasible order can be connected");

        var needsLandline = order.ConnectionType == ConnectionType.DialUp && !order.HasExistingLandline;
        var qty = order.Quantity;

        // ---- Check the equipment is available before changing anything ----------------------
        Product mainProduct = null, landlineProduct = null;
        var mainId = productId ?? order.ProductId;
        if (mainId != null)
        {
            mainProduct = await _db.Products.FirstOrDefaultAsync(p => p.Id == mainId && p.IsActive);
            if (mainProduct == null || !OrderService.ProductFits(order.ConnectionType, mainProduct.Category))
                return (null, "Choose equipment that suits this connection type");
            if (mainProduct.StockQty < qty) return (null, $"Only {mainProduct.StockQty} of {mainProduct.Name} in stock, {qty} needed");
        }
        if (needsLandline && landlineProductId != null)
        {
            landlineProduct = await _db.Products.FirstOrDefaultAsync(p => p.Id == landlineProductId && p.IsActive);
            if (landlineProduct == null || landlineProduct.Category != ProductCategory.Telephone)
                return (null, "Choose a telephone set for the landline");
            if (landlineProduct.StockQty < qty) return (null, $"Only {landlineProduct.StockQty} of {landlineProduct.Name} in stock, {qty} needed");
        }

        var deposits = await _db.DepositRates.AsNoTracking().ToListAsync();
        decimal DepositFor(ConnectionType t) => deposits.FirstOrDefault(d => d.ConnectionType == t)?.Amount ?? 0m;

        // One serial per line, matched to units in order. Blank lines are allowed.
        var serials = (serialNumbers ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        var created = new List<Connection>();
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            for (var i = 0; i < qty; i++)
            {
                string landlineAccount = order.ExistingLandlineAccountId;

                if (needsLandline)
                {
                    var t = NewConnection(order, ConnectionType.Telephone, order.LandlinePlan,
                        await _seq.AccountIdAsync(ConnectionType.Telephone, order.InstallCity.Code),
                        DepositFor(ConnectionType.Telephone), null);
                    if (landlineProduct != null)
                        t.Equipment.Add(new ConnectionEquipment { ProductId = landlineProduct.Id });
                    Log(t, "Activated", "Landline provided with dial-up order " + order.OrderNo, userId);
                    _db.Connections.Add(t);
                    created.Add(t);
                    landlineAccount = t.AccountId;
                }

                var main = NewConnection(order, order.ConnectionType, order.Plan,
                    await _seq.AccountIdAsync(order.ConnectionType, order.InstallCity.Code),
                    DepositFor(order.ConnectionType), order.ConnectionType == ConnectionType.DialUp ? landlineAccount : null);
                if (mainProduct != null)
                    main.Equipment.Add(new ConnectionEquipment
                    {
                        ProductId = mainProduct.Id,
                        SerialNo = i < serials.Count ? serials[i] : null
                    });
                Log(main, "Activated", "Connected from order " + order.OrderNo, userId);
                _db.Connections.Add(main);
                created.Add(main);
            }

            if (mainProduct != null) mainProduct.StockQty -= qty;
            if (landlineProduct != null) landlineProduct.StockQty -= qty;

            order.Status = OrderStatus.ConnectionProvided;
            order.StatusUpdatedOn = DateTime.Now;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        return (created, null);
    }

    private static Connection NewConnection(ServiceOrder order, ConnectionType type, Plan plan,
        string accountId, decimal grossDeposit, string linkedLandline)
    {
        return new Connection
        {
            AccountId = accountId,
            ConnectionType = type,
            CustomerId = order.CustomerId,
            ServiceOrderId = order.Id,
            PlanId = plan.Id,
            CityId = order.InstallCityId,
            InstallAddress = order.InstallAddress,
            Status = ConnectionStatus.Active,
            ActivatedOn = DateTime.Today,
            StatusChangedOn = DateTime.Now,
            DiscountPercent = order.DiscountPercent,
            SecurityDeposit = Pricing.NetDeposit(grossDeposit, order.DiscountPercent),
            LinkedLandlineAccountId = linkedLandline
        };
    }

    /// <summary>
    /// Active to temporarily inactive, temporarily inactive back to active,
    /// or either to permanently inactive (final). Permanent closure works out the deposit refund
    /// after any unpaid balance.
    /// </summary>
    public async Task<string> ChangeStatusAsync(int connectionId, ConnectionStatus target, string reason, int userId)
    {
        var c = await _db.Connections.Include(x => x.Bills).Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == connectionId);
        if (c == null) return "Connection not found";
        if (c.Status == ConnectionStatus.PermanentlyInactive) return "A permanently inactive connection cannot be changed";
        if (c.Status == target) return "The connection is already in that state";
        if (target != ConnectionStatus.Active && string.IsNullOrWhiteSpace(reason)) return "Give a reason";

        var previous = c.Status;
        c.Status = target;
        c.StatusChangedOn = DateTime.Now;
        c.StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        if (target == ConnectionStatus.PermanentlyInactive)
        {
            var outstanding = c.Bills.Where(b => !b.CarriedForward).Sum(b => b.Total - b.AmountPaid);
            c.DepositRefundAmount = Math.Max(0m, Pricing.Round(c.SecurityDeposit - outstanding));
            Log(c, "Closed permanently", $"{c.StatusReason}. Deposit refund due {c.DepositRefundAmount:0.00}", userId);
        }
        else if (target == ConnectionStatus.TemporarilyInactive)
            Log(c, "Suspended", c.StatusReason, userId);
        else
            Log(c, "Reactivated", "Back from " + (previous == ConnectionStatus.TemporarilyInactive ? "temporary suspension" : "inactive"), userId);

        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string> ChangePlanAsync(int connectionId, int newPlanId, int userId)
    {
        var c = await _db.Connections.Include(x => x.Plan).Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == connectionId);
        if (c == null) return "Connection not found";
        if (c.Status == ConnectionStatus.PermanentlyInactive) return "This connection is closed";
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == newPlanId && p.IsActive);
        if (plan == null || plan.ConnectionType != c.ConnectionType) return "Choose an active plan of the same connection type";
        if (plan.Id == c.PlanId) return "The connection is already on that plan";
        Log(c, "Plan changed", $"{c.Plan.Name} to {plan.Name}", userId);
        c.PlanId = plan.Id;
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>
    /// Swaps a unit for a new one from stock. When the customer spoiled the old unit,
    /// its replacement charge waits on the connection and goes onto the next bill.
    /// </summary>
    public async Task<string> ReplaceEquipmentAsync(int connectionId, int newProductId, string serial,
        bool damagedByCustomer, int userId)
    {
        var c = await _db.Connections.Include(x => x.Equipment).ThenInclude(e => e.Product)
            .Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == connectionId);
        if (c == null) return "Connection not found";
        if (c.Status == ConnectionStatus.PermanentlyInactive) return "This connection is closed";

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == newProductId && p.IsActive);
        if (product == null || !OrderService.ProductFits(c.ConnectionType, product.Category))
            return "Choose equipment that suits this connection type";
        if (product.StockQty < 1) return product.Name + " is out of stock";

        foreach (var old in c.Equipment.Where(e => e.ReturnedOn == null && e.Product.Category == product.Category))
            old.ReturnedOn = DateTime.Today;

        c.Equipment.Add(new ConnectionEquipment { ProductId = product.Id, SerialNo = serial?.Trim() });
        product.StockQty -= 1;

        if (damagedByCustomer && product.ReplacementCharge > 0)
            c.Charges.Add(new ConnectionCharge
            {
                Description = "Replacement of damaged " + product.Category.ToString().ToLowerInvariant() + ": " + product.Name,
                Amount = product.ReplacementCharge
            });

        Log(c, "Equipment replaced", product.Name + (damagedByCustomer ? " (customer damage, charged)" : ""), userId);
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string> MarkDepositRefundedAsync(int connectionId, int userId)
    {
        var c = await _db.Connections.Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == connectionId);
        if (c == null) return "Connection not found";
        if (c.DepositRefundAmount == null) return "No refund is due on this connection";
        if (c.DepositRefundedOn != null) return "Refund already paid";
        c.DepositRefundedOn = DateTime.Today;
        Log(c, "Deposit refunded", $"{c.DepositRefundAmount:0.00} returned to customer", userId);
        await _db.SaveChangesAsync();
        return null;
    }
}
