using Nexus.Web.Models;

namespace Nexus.Web.Services;

/// <summary>Keys for AppSetting rows, with the defaults taken from the specification.</summary>
public static class SettingKeys
{
    public const string TaxPercent = "Billing.TaxPercent";
    public const string DueDays = "Billing.DueDays";
    public const string MaxKmLandline = "Feasibility.MaxKm.Landline";
    public const string MaxKmBroadband = "Feasibility.MaxKm.Broadband";
    public const string MaxKmDialUp = "Feasibility.MaxKm.DialUp";

    public static readonly Dictionary<string, string> Defaults = new()
    {
        { TaxPercent, "12.24" },    // service tax, on the whole bill
        { DueDays, "15" },
        { MaxKmLandline, "5" },
        { MaxKmBroadband, "8" },
        { MaxKmDialUp, "10" }
    };
}

/// <summary>Everything a bill is calculated from.</summary>
public sealed class BillInput
{
    public decimal PlanCharge { get; init; }
    public decimal LocalMinutes { get; init; }
    public decimal StdMinutes { get; init; }
    public decimal MobileMsgMinutes { get; init; }
    public decimal? LocalRate { get; init; }
    public decimal? StdRate { get; init; }
    public decimal? MobileMsgRate { get; init; }
    public decimal ReplacementCharge { get; init; }
    public bool IsFirstBill { get; init; }
    public decimal DepositGross { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal TaxPercent { get; init; }
    public decimal PreviousDue { get; init; }
}

public sealed class BillResult
{
    public decimal PlanCharge { get; init; }
    public decimal UsageCharge { get; init; }
    public decimal ReplacementCharge { get; init; }
    public decimal SecurityDepositCharge { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal Subtotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal PreviousDue { get; init; }
    public decimal Total { get; init; }
}

/// <summary>Pure pricing rules. No database access, so they are easy to test.</summary>
public static class Pricing
{
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Bulk discount for a number of connections. Highest matching tier wins; no match means 0.</summary>
    public static decimal DiscountFor(int connections, IEnumerable<DiscountScheme> schemes)
    {
        var match = schemes
            .Where(s => connections >= s.MinConnections && (s.MaxConnections == null || connections <= s.MaxConnections))
            .OrderByDescending(s => s.MinConnections)
            .FirstOrDefault();
        return match?.Percent ?? 0m;
    }

    /// <summary>Deposit actually held after discount.</summary>
    public static decimal NetDeposit(decimal gross, decimal discountPercent) =>
        Round(gross - gross * discountPercent / 100m);

    /// <summary>
    /// Bill = rental + call charges + replacement charges + (first bill only: deposit) - (first bill only: discount)
    /// Tax is charged on that whole amount. Earlier unpaid balances are added afterwards, untaxed,
    /// because they were taxed when first billed.
    /// </summary>
    public static BillResult ComputeBill(BillInput i)
    {
        var usage = Round(i.LocalMinutes * (i.LocalRate ?? 0m)
                        + i.StdMinutes * (i.StdRate ?? 0m)
                        + i.MobileMsgMinutes * (i.MobileMsgRate ?? 0m));

        var deposit = i.IsFirstBill ? Round(i.DepositGross) : 0m;
        var discount = i.IsFirstBill
            ? Round((i.PlanCharge + i.DepositGross) * i.DiscountPercent / 100m)
            : 0m;

        var subtotal = Round(i.PlanCharge + usage + i.ReplacementCharge + deposit - discount);
        var tax = Round(subtotal * i.TaxPercent / 100m);
        var total = Round(subtotal + tax + i.PreviousDue);

        return new BillResult
        {
            PlanCharge = Round(i.PlanCharge),
            UsageCharge = usage,
            ReplacementCharge = Round(i.ReplacementCharge),
            SecurityDepositCharge = deposit,
            DiscountAmount = discount,
            Subtotal = subtotal,
            TaxAmount = tax,
            PreviousDue = Round(i.PreviousDue),
            Total = total
        };
    }

    public static BillStatus StatusFor(decimal total, decimal paid) =>
        paid <= 0 ? BillStatus.Unpaid : paid >= total ? BillStatus.Paid : BillStatus.PartiallyPaid;
}

/// <summary>Feasibility rules: distance from the exchange, and server availability for internet.</summary>
public static class FeasibilityRules
{
    /// <summary>
    /// What has to be checked for an order. Dial-up needs a landline, so unless the customer
    /// already holds one with Nexus, both the landline and the internet are checked.
    /// </summary>
    public static List<FeasibilitySubject> Required(ConnectionType type, bool hasExistingLandline)
    {
        return type switch
        {
            ConnectionType.Telephone => new List<FeasibilitySubject> { FeasibilitySubject.Landline },
            ConnectionType.Broadband => new List<FeasibilitySubject> { FeasibilitySubject.Internet },
            _ => hasExistingLandline
                ? new List<FeasibilitySubject> { FeasibilitySubject.Internet }
                : new List<FeasibilitySubject> { FeasibilitySubject.Internet, FeasibilitySubject.Landline }
        };
    }

    public static (bool Feasible, string Reason) Evaluate(
        FeasibilitySubject subject, decimal distanceKm, bool serverAvailable, decimal maxKm)
    {
        if (distanceKm > maxKm)
            return (false, $"Distance {distanceKm:0.##} km is beyond the {maxKm:0.##} km limit");
        if (subject == FeasibilitySubject.Internet && !serverAvailable)
            return (false, "No server capacity available on this exchange");
        return (true, "Within distance limit" + (subject == FeasibilitySubject.Internet ? " and server available" : ""));
    }

    /// <summary>Order status from the latest check of each required subject.</summary>
    public static OrderStatus Resolve(IEnumerable<FeasibilitySubject> required, IEnumerable<FeasibilityCheck> latestPerSubject)
    {
        var latest = latestPerSubject.ToList();
        if (latest.Count == 0) return OrderStatus.Placed;
        if (latest.Any(c => !c.IsFeasible)) return OrderStatus.NotFeasible;
        var allChecked = required.All(r => latest.Any(c => c.Subject == r));
        return allChecked ? OrderStatus.Feasible : OrderStatus.UnderFeasibilityCheck;
    }
}
