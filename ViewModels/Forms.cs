using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nexus.Web.Models;
using Nexus.Web.Services;

namespace Nexus.Web.ViewModels;

// ---------------------------------------------------------------- sign in and accounts
public class LoginVm
{
    [Required(ErrorMessage = "Enter your username")] public string Username { get; set; }
    [Required(ErrorMessage = "Enter your password"), DataType(DataType.Password)] public string Password { get; set; }
    public string ReturnUrl { get; set; }
}

public class RegisterVm
{
    [Required, StringLength(120), Display(Name = "Full name")] public string FullName { get; set; }
    public CustomerKind Kind { get; set; } = CustomerKind.Individual;
    [StringLength(120), Display(Name = "Company name")] public string CompanyName { get; set; }
    [Required, StringLength(40), RegularExpression(@"^[A-Za-z0-9._-]{4,40}$", ErrorMessage = "Use 4 to 40 letters, digits, dot, dash or underscore")]
    public string Username { get; set; }
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; }
    [Required, StringLength(20)] public string Phone { get; set; }
    [Required, StringLength(250)] public string Address { get; set; }
    [Required(ErrorMessage = "Choose your city"), Display(Name = "City")] public int? CityId { get; set; }
    [Required, DataType(DataType.Password)] public string Password { get; set; }
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match"), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; }
    public List<SelectListItem> Cities { get; set; } = new();
}

public class ChangePasswordVm
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")] public string Current { get; set; }
    [Required, DataType(DataType.Password), Display(Name = "New password")] public string New { get; set; }
    [Required, DataType(DataType.Password), Compare(nameof(New), ErrorMessage = "Passwords do not match"), Display(Name = "Repeat new password")]
    public string Repeat { get; set; }
}

// ---------------------------------------------------------------- customers
public class CustomerVm
{
    public int Id { get; set; }
    [Required, StringLength(120), Display(Name = "Full name")] public string FullName { get; set; }
    public CustomerKind Kind { get; set; } = CustomerKind.Individual;
    [StringLength(120), Display(Name = "Company name")] public string CompanyName { get; set; }
    [EmailAddress, StringLength(160)] public string Email { get; set; }
    [Required, StringLength(20)] public string Phone { get; set; }
    [Required, StringLength(250)] public string Address { get; set; }
    [Required(ErrorMessage = "Choose a city"), Display(Name = "City")] public int? CityId { get; set; }
    [StringLength(40), Display(Name = "ID proof type")] public string IdProofType { get; set; }
    [StringLength(60), Display(Name = "ID proof number")] public string IdProofNumber { get; set; }

    // Optional portal login, offered when a retail employee registers a walk-in customer.
    [Display(Name = "Create a portal login")] public bool CreateLogin { get; set; }
    public string Username { get; set; }
    [DataType(DataType.Password)] public string Password { get; set; }

    public List<SelectListItem> Cities { get; set; } = new();
}

// ---------------------------------------------------------------- orders
public class OrderFormVm
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    [Display(Name = "Connection type")] public ConnectionType ConnectionType { get; set; } = ConnectionType.Broadband;
    [Required(ErrorMessage = "Choose a plan"), Display(Name = "Plan")] public int? PlanId { get; set; }
    [Display(Name = "Equipment")] public int? ProductId { get; set; }
    [Range(1, 500), Display(Name = "Number of connections")] public int Quantity { get; set; } = 1;
    [Display(Name = "I already hold a Nexus landline")] public bool HasExistingLandline { get; set; }
    [Display(Name = "Landline account id")] public string ExistingLandlineAccountId { get; set; }
    [Display(Name = "Landline plan")] public int? LandlinePlanId { get; set; }
    [Required(ErrorMessage = "Choose a city"), Display(Name = "City")] public int? InstallCityId { get; set; }
    [Required, StringLength(250), Display(Name = "Installation address")] public string InstallAddress { get; set; }
    [StringLength(300)] public string Remarks { get; set; }
    public IFormFile AddressProof { get; set; }
    public IFormFile ApplicationForm { get; set; }

    // Lists for the form (not posted back).
    public List<Plan> Plans { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public List<City> Cities { get; set; } = new();
    public List<DiscountScheme> Schemes { get; set; } = new();
    public List<SecurityDepositRate> Deposits { get; set; } = new();
}

public class FeasibilityVm
{
    public int OrderId { get; set; }
    public FeasibilitySubject Subject { get; set; }
    [Range(0, 1000, ErrorMessage = "Enter the distance in kilometres")] public decimal DistanceKm { get; set; }
    public bool ServerAvailable { get; set; }
    [StringLength(250)] public string Remarks { get; set; }
}

public class ProvisionVm
{
    public int OrderId { get; set; }
    public int? ProductId { get; set; }
    public int? LandlineProductId { get; set; }
    public string Serials { get; set; }
}

public class StatusChangeVm
{
    public int Id { get; set; }
    public ConnectionStatus Target { get; set; }
    [StringLength(250)] public string Reason { get; set; }
}

public class PlanChangeVm { public int Id { get; set; } public int PlanId { get; set; } }

public class ReplaceEquipmentVm
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    [StringLength(60)] public string Serial { get; set; }
    public bool DamagedByCustomer { get; set; }
}

// ---------------------------------------------------------------- billing
public class BillFormVm
{
    public int ConnectionId { get; set; }
    [DataType(DataType.Date), Display(Name = "Period starts")] public DateTime PeriodStart { get; set; } = DateTime.Today;
    [Range(0, 1000000), Display(Name = "Local minutes")] public decimal LocalMinutes { get; set; }
    [Range(0, 1000000), Display(Name = "STD minutes")] public decimal StdMinutes { get; set; }
    [Range(0, 1000000), Display(Name = "Messaging minutes")] public decimal MobileMsgMinutes { get; set; }
    [StringLength(300)] public string Notes { get; set; }
    public string Intent { get; set; }

    // For display and preview.
    public Connection Connection { get; set; }
    public BillResult Preview { get; set; }
    public DateTime PreviewPeriodEnd { get; set; }
    public decimal Outstanding { get; set; }
    public List<ConnectionCharge> PendingCharges { get; set; } = new();
}

public class PaymentVm
{
    public int BillId { get; set; }
    [Range(0.01, 1000000, ErrorMessage = "Enter an amount above zero")] public decimal Amount { get; set; }
    public PaymentMode Mode { get; set; } = PaymentMode.Cash;
    [StringLength(60)] public string Reference { get; set; }
    [DataType(DataType.Date), Display(Name = "Paid on")] public DateTime PaidOn { get; set; } = DateTime.Today;
}

// ---------------------------------------------------------------- admin
public class EmployeeVm
{
    public int Id { get; set; }
    [Required, StringLength(120), Display(Name = "Full name")] public string FullName { get; set; }
    [Required, StringLength(40), RegularExpression(@"^[A-Za-z0-9._-]{4,40}$", ErrorMessage = "Use 4 to 40 letters, digits, dot, dash or underscore")]
    public string Username { get; set; }
    [Required, EmailAddress, StringLength(160)] public string Email { get; set; }
    [Required, StringLength(20)] public string Phone { get; set; }
    [StringLength(200)] public string Address { get; set; }
    [Required] public string Role { get; set; } = Roles.Retail;
    [Display(Name = "Retail shop")] public int? RetailShopId { get; set; }
    [DataType(DataType.Date), Display(Name = "Hire date")] public DateTime HireDate { get; set; } = DateTime.Today;
    public bool IsActive { get; set; } = true;
    [DataType(DataType.Password)] public string Password { get; set; }
    public List<SelectListItem> Shops { get; set; } = new();
}

public class SettingsVm
{
    [Range(0, 100), Display(Name = "Service tax (%)")] public decimal TaxPercent { get; set; }
    [Range(1, 120), Display(Name = "Days until a bill is due")] public int DueDays { get; set; }
    [Range(0, 1000), Display(Name = "Landline limit (km)")] public decimal MaxKmLandline { get; set; }
    [Range(0, 1000), Display(Name = "Broadband limit (km)")] public decimal MaxKmBroadband { get; set; }
    [Range(0, 1000), Display(Name = "Dial-up limit (km)")] public decimal MaxKmDialUp { get; set; }
}

public class DepositsVm
{
    [Range(0, 100000), Display(Name = "Dial-up")] public decimal DialUp { get; set; }
    [Range(0, 100000), Display(Name = "Broadband")] public decimal Broadband { get; set; }
    [Range(0, 100000), Display(Name = "Landline")] public decimal Telephone { get; set; }
}

public class PurchaseLineVm
{
    public int ProductId { get; set; }
    [Range(0, 1000000)] public int Quantity { get; set; }
    [Range(0, 1000000)] public decimal UnitCost { get; set; }
}

public class PurchaseFormVm
{
    [Required(ErrorMessage = "Choose a vendor")] public int? VendorId { get; set; }
    [StringLength(300)] public string Notes { get; set; }
    public List<PurchaseLineVm> Lines { get; set; } = new();
    public List<Vendor> Vendors { get; set; } = new();
    public List<Product> Products { get; set; } = new();
}

public class FeedbackVm
{
    public int? ServiceOrderId { get; set; }
    [Range(1, 5)] public int Rating { get; set; } = 5;
    [Required(ErrorMessage = "Write a few words"), StringLength(600)] public string Comments { get; set; }
    public List<ServiceOrder> Orders { get; set; } = new();
}

// ---------------------------------------------------------------- search
public class SearchVm
{
    public string Target { get; set; } = "orders";           // orders | connections
    [Display(Name = "Order or account id")] public string Id { get; set; }
    public string Name { get; set; }
    public ConnectionType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string Phone { get; set; }
    public bool Submitted { get; set; }
    public List<ServiceOrder> Orders { get; set; } = new();
    public List<Connection> Connections { get; set; } = new();
}

// ---------------------------------------------------------------- tracking (public)
public class TrackVm
{
    public string Query { get; set; }
    public string Message { get; set; }
    public ServiceOrder Order { get; set; }
    public Connection Connection { get; set; }
    public decimal Due { get; set; }
    public DateTime? NextDue { get; set; }
    public bool Overdue { get; set; }
}
