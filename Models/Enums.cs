using System.ComponentModel.DataAnnotations;

namespace Nexus.Web.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Accounts = "Accounts";
    public const string Technical = "Technical";
    public const string Retail = "Retail";
    public const string Customer = "Customer";

    public const string Staff = "Admin,Accounts,Technical,Retail";
    public static readonly string[] EmployeeRoles = { Admin, Accounts, Technical, Retail };
}

public enum ConnectionType
{
    [Display(Name = "Dial-up")] DialUp = 1,
    [Display(Name = "Broadband")] Broadband = 2,
    [Display(Name = "Telephone")] Telephone = 3
}

public enum PlanCategory
{
    [Display(Name = "Hourly")] Hourly = 1,
    [Display(Name = "Unlimited")] Unlimited = 2,
    [Display(Name = "Local landline")] LocalLandline = 3,
    [Display(Name = "STD landline")] StdLandline = 4
}

public enum OrderStatus
{
    [Display(Name = "Placed")] Placed = 1,
    [Display(Name = "Feasibility check")] UnderFeasibilityCheck = 2,
    [Display(Name = "Feasible")] Feasible = 3,
    [Display(Name = "Not feasible")] NotFeasible = 4,
    [Display(Name = "Connected")] ConnectionProvided = 5,
    [Display(Name = "Cancelled")] Cancelled = 6
}

public enum ConnectionStatus
{
    [Display(Name = "Active")] Active = 1,
    [Display(Name = "Temporarily inactive")] TemporarilyInactive = 2,
    [Display(Name = "Permanently inactive")] PermanentlyInactive = 3
}

public enum BillStatus
{
    [Display(Name = "Unpaid")] Unpaid = 1,
    [Display(Name = "Part paid")] PartiallyPaid = 2,
    [Display(Name = "Paid")] Paid = 3
}

public enum PaymentMode
{
    [Display(Name = "Cash")] Cash = 1,
    [Display(Name = "Card")] Card = 2,
    [Display(Name = "Cheque")] Cheque = 3,
    [Display(Name = "Bank transfer")] BankTransfer = 4
}

public enum CustomerKind
{
    [Display(Name = "Individual")] Individual = 1,
    [Display(Name = "Corporate")] Corporate = 2
}

public enum DocumentKind
{
    [Display(Name = "Address proof")] AddressProof = 1,
    [Display(Name = "Application form")] ApplicationForm = 2,
    [Display(Name = "Receipt")] Receipt = 3,
    [Display(Name = "Other")] Other = 4
}

public enum ProductCategory
{
    [Display(Name = "Modem")] Modem = 1,
    [Display(Name = "Router")] Router = 2,
    [Display(Name = "Telephone set")] Telephone = 3
}

public enum PurchaseStatus
{
    [Display(Name = "Ordered")] Ordered = 1,
    [Display(Name = "Received")] Received = 2,
    [Display(Name = "Cancelled")] Cancelled = 3
}

public enum OrderChannel
{
    [Display(Name = "Retail shop")] RetailShop = 1,
    [Display(Name = "Online")] Online = 2
}

public enum FeasibilitySubject
{
    [Display(Name = "Landline")] Landline = 1,
    [Display(Name = "Internet")] Internet = 2
}
