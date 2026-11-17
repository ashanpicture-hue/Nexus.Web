using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;

namespace Nexus.Web.Services;

/// <summary>What a person fills in to apply for a connection.</summary>
public class PlaceOrderRequest
{
    public int CustomerId { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public int PlanId { get; set; }
    public int? ProductId { get; set; }
    public int Quantity { get; set; } = 1;
    public bool HasExistingLandline { get; set; }
    public string ExistingLandlineAccountId { get; set; }
    public int? LandlinePlanId { get; set; }
    public int InstallCityId { get; set; }
    public string InstallAddress { get; set; }
    public string Remarks { get; set; }
    public OrderChannel Channel { get; set; }
    public int? RetailShopId { get; set; }
    public int PlacedById { get; set; }
    public List<(DocumentKind Kind, IFormFile File)> Documents { get; set; } = new();
}

public class OrderResult
{
    public ServiceOrder Order { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Ok => Errors.Count == 0;
}

/// <summary>Placing orders and recording feasibility checks.</summary>
public class OrderService
{
    private readonly AppDbContext _db;
    private readonly SequenceService _seq;
    private readonly SettingsService _settings;
    private readonly FileStore _files;

    public OrderService(AppDbContext db, SequenceService seq, SettingsService settings, FileStore files)
    {
        _db = db; _seq = seq; _settings = settings; _files = files;
    }

    /// <summary>The kind of equipment each connection type is delivered with.</summary>
    public static bool ProductFits(ConnectionType type, ProductCategory cat) => type switch
    {
        ConnectionType.DialUp => cat == ProductCategory.Modem,
        ConnectionType.Broadband => cat == ProductCategory.Modem || cat == ProductCategory.Router,
        _ => cat == ProductCategory.Telephone
    };

    public async Task<OrderResult> PlaceAsync(PlaceOrderRequest r)
    {
        var result = new OrderResult();

        // ---- Validate the request against the master data -----------------------------------
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == r.CustomerId);
        if (customer == null) { result.Errors.Add("Customer not found"); return result; }

        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == r.PlanId && p.IsActive);
        if (plan == null || plan.ConnectionType != r.ConnectionType)
            result.Errors.Add("Choose a plan that belongs to the selected connection type");

        var city = await _db.Cities.FirstOrDefaultAsync(c => c.Id == r.InstallCityId);
        if (city == null) result.Errors.Add("Choose the city for the connection");

        if (r.Quantity < 1 || r.Quantity > 500) result.Errors.Add("Number of connections must be between 1 and 500");

        if (r.ProductId != null)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == r.ProductId && p.IsActive);
            if (product == null || !ProductFits(r.ConnectionType, product.Category))
                result.Errors.Add("That equipment does not suit this connection type");
        }

        if (r.ConnectionType == ConnectionType.DialUp)
        {
            if (r.HasExistingLandline)
            {
                // Dial-up needs a Nexus landline. Verify the one the customer quotes.
                var acc = IdFormat.Normalize(r.ExistingLandlineAccountId);
                r.ExistingLandlineAccountId = acc;
                var landline = await _db.Connections.FirstOrDefaultAsync(c => c.AccountId == acc);
                if (landline == null || landline.ConnectionType != ConnectionType.Telephone)
                    result.Errors.Add("No telephone connection found for that account id");
                else if (landline.CustomerId != r.CustomerId)
                    result.Errors.Add("That landline belongs to a different customer");
                else if (landline.Status == ConnectionStatus.PermanentlyInactive)
                    result.Errors.Add("That landline is permanently inactive");
                if (r.Quantity != 1)
                    result.Errors.Add("An existing landline can carry one dial-up connection. Use a separate order for each");
            }
            else
            {
                var lp = await _db.Plans.FirstOrDefaultAsync(p => p.Id == r.LandlinePlanId && p.IsActive);
                if (lp == null || lp.ConnectionType != ConnectionType.Telephone)
                    result.Errors.Add("Dial-up needs a landline. Choose a landline plan, or quote an existing landline");
            }
        }
        else
        {
            r.HasExistingLandline = false;
            r.ExistingLandlineAccountId = null;
            r.LandlinePlanId = null;
        }

        foreach (var doc in r.Documents)
        {
            var err = await FileStore.ValidateAsync(doc.File);
            if (err != null) result.Errors.Add(err);
        }

        if (!result.Ok) return result;

        // ---- Create the order ---------------------------------------------------------------
        var schemes = await _db.DiscountSchemes.AsNoTracking().ToListAsync();
        var order = new ServiceOrder
        {
            OrderNo = await _seq.OrderNoAsync(r.ConnectionType),
            ConnectionType = r.ConnectionType,
            CustomerId = r.CustomerId,
            PlanId = r.PlanId,
            ProductId = r.ProductId,
            Quantity = r.Quantity,
            HasExistingLandline = r.HasExistingLandline,
            ExistingLandlineAccountId = r.ExistingLandlineAccountId,
            LandlinePlanId = r.HasExistingLandline ? null : r.LandlinePlanId,
            InstallCityId = r.InstallCityId,
            InstallAddress = r.InstallAddress?.Trim(),
            Channel = r.Channel,
            RetailShopId = r.RetailShopId,
            PlacedById = r.PlacedById,
            Remarks = r.Remarks?.Trim(),
            Status = OrderStatus.Placed,
            DiscountPercent = Pricing.DiscountFor(r.Quantity, schemes)
        };
        _db.ServiceOrders.Add(order);
        await _db.SaveChangesAsync();

        // ---- File the paperwork by year and area --------------------------------------------
        foreach (var (kind, file) in r.Documents)
        {
            if (file == null || file.Length == 0) continue;
            var path = await _files.SaveAsync(file, order.PlacedOn.Year, city.Code, order.OrderNo);
            _db.OrderDocuments.Add(new OrderDocument
            {
                ServiceOrderId = order.Id,
                CustomerId = order.CustomerId,
                Kind = kind,
                OriginalName = Path.GetFileName(file.FileName),
                StoredPath = path,
                UploadedById = r.PlacedById
            });
        }
        await _db.SaveChangesAsync();

        result.Order = order;
        return result;
    }

    /// <summary>
    /// Records a feasibility check for one subject (landline or internet) and moves the order
    /// to Feasibility check, Feasible or Not feasible. A later check of the same subject replaces an earlier one.
    /// </summary>
    public async Task<string> RecordFeasibilityAsync(int orderId, FeasibilitySubject subject,
        decimal distanceKm, bool serverAvailable, string remarks, int userId)
    {
        var order = await _db.ServiceOrders.Include(o => o.FeasibilityChecks).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return "Order not found";
        if (order.Status == OrderStatus.ConnectionProvided || order.Status == OrderStatus.Cancelled)
            return "This order is closed";

        var required = FeasibilityRules.Required(order.ConnectionType, order.HasExistingLandline);
        if (!required.Contains(subject)) return "That check is not needed for this order";

        var maxKm = subject == FeasibilitySubject.Landline
            ? await _settings.GetDecimalAsync(SettingKeys.MaxKmLandline)
            : order.ConnectionType == ConnectionType.Broadband
                ? await _settings.GetDecimalAsync(SettingKeys.MaxKmBroadband)
                : await _settings.GetDecimalAsync(SettingKeys.MaxKmDialUp);

        var verdict = FeasibilityRules.Evaluate(subject, distanceKm, serverAvailable, maxKm);
        var check = new FeasibilityCheck
        {
            ServiceOrderId = order.Id,
            Subject = subject,
            DistanceKm = distanceKm,
            ServerAvailable = subject == FeasibilitySubject.Internet ? serverAvailable : true,
            IsFeasible = verdict.Feasible,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? verdict.Reason : remarks.Trim() + ". " + verdict.Reason,
            CheckedById = userId
        };
        _db.FeasibilityChecks.Add(check);
        order.FeasibilityChecks.Add(check);

        var latest = order.FeasibilityChecks
            .GroupBy(c => c.Subject)
            .Select(g => g.OrderByDescending(c => c.CheckedOn).ThenByDescending(c => c.Id).First())
            .ToList();
        var newStatus = FeasibilityRules.Resolve(required, latest);
        if (newStatus != order.Status)
        {
            order.Status = newStatus;
            order.StatusUpdatedOn = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>Marks an order as being looked at by the technical team.</summary>
    public async Task<string> StartCheckAsync(int orderId)
    {
        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return "Order not found";
        if (order.Status != OrderStatus.Placed) return "Order is already past this stage";
        order.Status = OrderStatus.UnderFeasibilityCheck;
        order.StatusUpdatedOn = DateTime.Now;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string> CancelAsync(int orderId, string reason)
    {
        var order = await _db.ServiceOrders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return "Order not found";
        if (order.Status == OrderStatus.ConnectionProvided || order.Status == OrderStatus.Cancelled)
            return "This order can no longer be cancelled";
        order.Status = OrderStatus.Cancelled;
        order.StatusUpdatedOn = DateTime.Now;
        order.Remarks = string.IsNullOrWhiteSpace(reason) ? order.Remarks : (order.Remarks + " Cancelled: " + reason.Trim()).Trim();
        await _db.SaveChangesAsync();
        return null;
    }
}
