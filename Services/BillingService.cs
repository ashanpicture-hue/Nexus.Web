using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;

namespace Nexus.Web.Services;

public class GenerateBillRequest
{
    public int ConnectionId { get; set; }
    public DateTime PeriodStart { get; set; }
    public decimal LocalMinutes { get; set; }
    public decimal StdMinutes { get; set; }
    public decimal MobileMsgMinutes { get; set; }
    public string Notes { get; set; }
}

public class BillingService
{
    private readonly AppDbContext _db;
    private readonly SequenceService _seq;
    private readonly SettingsService _settings;

    public BillingService(AppDbContext db, SequenceService seq, SettingsService settings)
    {
        _db = db; _seq = seq; _settings = settings;
    }

    public async Task<decimal> OutstandingAsync(int connectionId)
    {
        var bills = await _db.Bills.AsNoTracking()
            .Where(b => b.ConnectionId == connectionId && !b.CarriedForward).ToListAsync();
        return bills.Sum(b => b.Total - b.AmountPaid);
    }

    public async Task<DateTime> NextPeriodStartAsync(int connectionId)
    {
        var last = await _db.Bills.AsNoTracking().Where(b => b.ConnectionId == connectionId)
            .OrderByDescending(b => b.PeriodEnd).FirstOrDefaultAsync();
        if (last != null) return last.PeriodEnd.AddDays(1);
        var c = await _db.Connections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == connectionId);
        return c?.ActivatedOn ?? DateTime.Today;
    }

    public async Task<(Bill Bill, string Error)> GenerateAsync(GenerateBillRequest r, int userId)
    {
        var c = await _db.Connections.Include(x => x.Plan).Include(x => x.Bills).Include(x => x.Charges)
            .FirstOrDefaultAsync(x => x.Id == r.ConnectionId);
        if (c == null) return (null, "Connection not found");
        if (c.Status == ConnectionStatus.PermanentlyInactive) return (null, "This connection is permanently inactive");
        if (r.LocalMinutes < 0 || r.StdMinutes < 0 || r.MobileMsgMinutes < 0) return (null, "Minutes cannot be negative");

        var lastEnd = c.Bills.Count == 0 ? (DateTime?)null : c.Bills.Max(b => b.PeriodEnd);
        if (lastEnd != null && r.PeriodStart.Date <= lastEnd.Value.Date)
            return (null, "That period overlaps an earlier bill. The next period starts " + lastEnd.Value.AddDays(1).ToString("d MMM yyyy"));

        var taxPercent = await _settings.GetDecimalAsync(SettingKeys.TaxPercent);
        var dueDays = await _settings.GetIntAsync(SettingKeys.DueDays);
        var isFirst = c.Bills.Count == 0;

        var depositRate = await _db.DepositRates.AsNoTracking().FirstOrDefaultAsync(d => d.ConnectionType == c.ConnectionType);
        var openBills = c.Bills.Where(b => !b.CarriedForward && b.Total - b.AmountPaid > 0).ToList();
        var unbilled = c.Charges.Where(x => x.BilledOnBillId == null).ToList();

        var plan = c.Plan;
        var calc = Pricing.ComputeBill(new BillInput
        {
            PlanCharge = plan.Price,
            LocalMinutes = plan.LocalRate != null ? r.LocalMinutes : 0,
            StdMinutes = plan.StdRate != null ? r.StdMinutes : 0,
            MobileMsgMinutes = plan.MobileMsgRate != null ? r.MobileMsgMinutes : 0,
            LocalRate = plan.LocalRate,
            StdRate = plan.StdRate,
            MobileMsgRate = plan.MobileMsgRate,
            ReplacementCharge = unbilled.Sum(x => x.Amount),
            IsFirstBill = isFirst,
            DepositGross = depositRate?.Amount ?? 0m,
            DiscountPercent = c.DiscountPercent,
            TaxPercent = taxPercent,
            PreviousDue = openBills.Sum(b => b.Total - b.AmountPaid)
        });

        var bill = new Bill
        {
            BillNo = await _seq.BillNoAsync(),
            ConnectionId = c.Id,
            PeriodStart = r.PeriodStart.Date,
            PeriodEnd = r.PeriodStart.Date.AddMonths(plan.ValidityMonths).AddDays(-1),
            IssuedOn = DateTime.Today,
            DueDate = DateTime.Today.AddDays(dueDays),
            IsFirstBill = isFirst,
            PlanCharge = calc.PlanCharge,
            LocalMinutes = plan.LocalRate != null ? r.LocalMinutes : 0,
            StdMinutes = plan.StdRate != null ? r.StdMinutes : 0,
            MobileMsgMinutes = plan.MobileMsgRate != null ? r.MobileMsgMinutes : 0,
            UsageCharge = calc.UsageCharge,
            ReplacementCharge = calc.ReplacementCharge,
            SecurityDepositCharge = calc.SecurityDepositCharge,
            DiscountAmount = calc.DiscountAmount,
            Subtotal = calc.Subtotal,
            TaxPercent = taxPercent,
            TaxAmount = calc.TaxAmount,
            PreviousDue = calc.PreviousDue,
            Total = calc.Total,
            Notes = r.Notes?.Trim(),
            GeneratedById = userId
        };
        bill.Status = bill.Total <= 0 ? BillStatus.Paid : BillStatus.Unpaid;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Bills.Add(bill);
            foreach (var old in openBills) old.CarriedForward = true;
            await _db.SaveChangesAsync();
            foreach (var ch in unbilled) ch.BilledOnBillId = bill.Id;
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
        return (bill, null);
    }

public async Task<(Payment Payment, string Error)> PayAsync(int billId, decimal amount, PaymentMode mode,
        string reference, DateTime paidOn, int userId, int? shopId)
    {
        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == billId);
        if (bill == null) return (null, "Bill not found");
        if (bill.CarriedForward) return (null, "This bill's balance was carried into a later bill. Take the payment there");
        amount = Pricing.Round(amount);
        if (amount <= 0) return (null, "Enter an amount above zero");
        if (amount > bill.Due) return (null, $"The amount is more than the {bill.Due:0.00} due");
        if (paidOn.Date > DateTime.Today) return (null, "Payment date cannot be in the future");

        var payment = new Payment
        {
            ReceiptNo = await _seq.ReceiptNoAsync(),
            BillId = bill.Id,
            Amount = amount,
            Mode = mode,
            Reference = reference?.Trim(),
            PaidOn = paidOn.Date,
            ReceivedById = userId,
            RetailShopId = shopId
        };
        _db.Payments.Add(payment);
        bill.AmountPaid += amount;
        bill.Status = Pricing.StatusFor(bill.Total, bill.AmountPaid);
        await _db.SaveChangesAsync();
        return (payment, null);
    }
}
