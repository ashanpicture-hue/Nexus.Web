using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexus.Web.Models;

public class City
{
    public int Id { get; set; }
    [Required, StringLength(80)] public string Name { get; set; }
    [Required, RegularExpression(@"^\d{3}$", ErrorMessage = "Use exactly three digits")]
    [StringLength(3)] public string Code { get; set; }
}

public class AppUser
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Username { get; set; }
    [Required] public string PasswordHash { get; set; }
    [Required, StringLength(20)] public string Role { get; set; }
    public bool IsActive { get; set; } = true;
    [Required, StringLength(120)] public string FullName { get; set; }
    [EmailAddress, StringLength(160)] public string Email { get; set; }
    [StringLength(20)] public string Phone { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public int FailedLogins { get; set; }
    public DateTime? LockedUntil { get; set; }
}

public class Employee
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; }
    [StringLength(20)] public string EmployeeNo { get; set; }
    [StringLength(200)] public string Address { get; set; }
    public DateTime HireDate { get; set; } = DateTime.Today;
        public int? RetailShopId { get; set; }
    public RetailShop RetailShop { get; set; }
}

public class RetailShop
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; }
    public int CityId { get; set; }
    public City City { get; set; }
    [Required, StringLength(250)] public string Address { get; set; }
    [StringLength(20)] public string Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Customer
{
    public int Id { get; set; }
        public int? UserId { get; set; }
    public AppUser User { get; set; }
    [Required, StringLength(120)] public string FullName { get; set; }
    public CustomerKind Kind { get; set; } = CustomerKind.Individual;
    [StringLength(120)] public string CompanyName { get; set; }
    [EmailAddress, StringLength(160)] public string Email { get; set; }
    [Required, StringLength(20)] public string Phone { get; set; }
    [Required, StringLength(250)] public string Address { get; set; }
    public int CityId { get; set; }
    public City City { get; set; }
    [StringLength(40)] public string IdProofType { get; set; }
    [StringLength(60)] public string IdProofNumber { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.Now;
}

public class Plan
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public PlanCategory Category { get; set; }
    public int? SpeedKbps { get; set; }
    public int? Hours { get; set; }
        [Range(1, 60)] public int ValidityMonths { get; set; } = 1;
    [Range(0, 100000)] public decimal Price { get; set; }
    [Range(0, 100)] public decimal? LocalRate { get; set; }
    [Range(0, 100)] public decimal? StdRate { get; set; }
    [Range(0, 100)] public decimal? MobileMsgRate { get; set; }
    [StringLength(400)] public string Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SecurityDepositRate
{
    public int Id { get; set; }
    public ConnectionType ConnectionType { get; set; }
    [Range(0, 100000)] public decimal Amount { get; set; }
}

public class DiscountScheme
{
    public int Id { get; set; }
    [Required, StringLength(60)] public string Name { get; set; }
    [Range(1, 100000)] public int MinConnections { get; set; }
        public int? MaxConnections { get; set; }
    [Range(0, 100)] public decimal Percent { get; set; }
}

public class AppSetting
{
    [Key, StringLength(80)] public string Key { get; set; }
    [Required, StringLength(200)] public string Value { get; set; }
}

public class Vendor
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; }
    [StringLength(120)] public string ContactPerson { get; set; }
    [Required, StringLength(20)] public string Phone { get; set; }
    [EmailAddress, StringLength(160)] public string Email { get; set; }
    [StringLength(250)] public string Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Product
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; }
    public ProductCategory Category { get; set; }
    [StringLength(60)] public string Model { get; set; }
    public int? VendorId { get; set; }
    public Vendor Vendor { get; set; }
        [Range(0, 100000)] public decimal ReplacementCharge { get; set; }
    [Range(0, 1000000)] public int StockQty { get; set; }
    [Range(0, 1000000)] public int ReorderLevel { get; set; } = 10;
    [StringLength(300)] public string Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PurchaseOrder
{
    public int Id { get; set; }
    [StringLength(20)] public string PoNumber { get; set; }
    public int VendorId { get; set; }
    public Vendor Vendor { get; set; }
    public DateTime OrderedOn { get; set; } = DateTime.Today;
    public DateTime? ReceivedOn { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Ordered;
    [StringLength(300)] public string Notes { get; set; }
    public int CreatedById { get; set; }
    public List<PurchaseOrderLine> Lines { get; set; } = new();
}

public class PurchaseOrderLine
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    [Range(1, 1000000)] public int Quantity { get; set; }
    [Range(0, 1000000)] public decimal UnitCost { get; set; }
}

public class ServiceOrder
{
    public int Id { get; set; }
    [StringLength(11)] public string OrderNo { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public int PlanId { get; set; }
    public Plan Plan { get; set; }
        public int? ProductId { get; set; }
    public Product Product { get; set; }
    public int Quantity { get; set; } = 1;
        public bool HasExistingLandline { get; set; }
    [StringLength(16)] public string ExistingLandlineAccountId { get; set; }
        public int? LandlinePlanId { get; set; }
    public Plan LandlinePlan { get; set; }
    public int InstallCityId { get; set; }
    public City InstallCity { get; set; }
    [Required, StringLength(250)] public string InstallAddress { get; set; }
    public OrderChannel Channel { get; set; }
    public int? RetailShopId { get; set; }
    public RetailShop RetailShop { get; set; }
    public int PlacedById { get; set; }
    public DateTime PlacedOn { get; set; } = DateTime.Now;
    public OrderStatus Status { get; set; } = OrderStatus.Placed;
    public DateTime StatusUpdatedOn { get; set; } = DateTime.Now;
        public decimal DiscountPercent { get; set; }
    [StringLength(300)] public string Remarks { get; set; }
    public List<FeasibilityCheck> FeasibilityChecks { get; set; } = new();
    public List<Connection> Connections { get; set; } = new();
    public List<OrderDocument> Documents { get; set; } = new();
}

public class FeasibilityCheck
{
    public int Id { get; set; }
    public int ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; }
    public FeasibilitySubject Subject { get; set; }
    [Range(0, 1000)] public decimal DistanceKm { get; set; }
    public bool ServerAvailable { get; set; }
    public bool IsFeasible { get; set; }
    [StringLength(300)] public string Remarks { get; set; }
    public int CheckedById { get; set; }
    public DateTime CheckedOn { get; set; } = DateTime.Now;
}

public class Connection
{
    public int Id { get; set; }
    [StringLength(16)] public string AccountId { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public int ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; }
    public int PlanId { get; set; }
    public Plan Plan { get; set; }
    public int CityId { get; set; }
    public City City { get; set; }
    [Required, StringLength(250)] public string InstallAddress { get; set; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Active;
    public DateTime ActivatedOn { get; set; } = DateTime.Today;
    public DateTime StatusChangedOn { get; set; } = DateTime.Now;
    [StringLength(300)] public string StatusReason { get; set; }
    public decimal DiscountPercent { get; set; }
        public decimal SecurityDeposit { get; set; }
    public decimal? DepositRefundAmount { get; set; }
    public DateTime? DepositRefundedOn { get; set; }
        [StringLength(16)] public string LinkedLandlineAccountId { get; set; }
    public List<ConnectionEquipment> Equipment { get; set; } = new();
    public List<ConnectionEvent> Events { get; set; } = new();
    public List<ConnectionCharge> Charges { get; set; } = new();
    public List<Bill> Bills { get; set; } = new();
}

public class ConnectionEquipment
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public Connection Connection { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    [StringLength(60)] public string SerialNo { get; set; }
    public DateTime IssuedOn { get; set; } = DateTime.Today;
    public DateTime? ReturnedOn { get; set; }
}

public class ConnectionEvent
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public Connection Connection { get; set; }
    [Required, StringLength(40)] public string Event { get; set; }
    [StringLength(300)] public string Detail { get; set; }
    public int? ByUserId { get; set; }
    public DateTime At { get; set; } = DateTime.Now;
}

public class ConnectionCharge
{
    public int Id { get; set; }
    public int ConnectionId { get; set; }
    public Connection Connection { get; set; }
    [Required, StringLength(200)] public string Description { get; set; }
    [Range(0, 100000)] public decimal Amount { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.Now;
    public int? BilledOnBillId { get; set; }
}

public class Bill
{
    public int Id { get; set; }
    [StringLength(20)] public string BillNo { get; set; }
    public int ConnectionId { get; set; }
    public Connection Connection { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime IssuedOn { get; set; } = DateTime.Today;
    public DateTime DueDate { get; set; }
    public bool IsFirstBill { get; set; }

    public decimal PlanCharge { get; set; }
    public decimal LocalMinutes { get; set; }
    public decimal StdMinutes { get; set; }
    public decimal MobileMsgMinutes { get; set; }
    public decimal UsageCharge { get; set; }
    public decimal ReplacementCharge { get; set; }
    public decimal SecurityDepositCharge { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal PreviousDue { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Unpaid;
        public bool CarriedForward { get; set; }
    [StringLength(300)] public string Notes { get; set; }
    public int GeneratedById { get; set; }
    public List<Payment> Payments { get; set; } = new();

    [NotMapped] public decimal Due => Total - AmountPaid;
}

public class Payment
{
    public int Id { get; set; }
    [StringLength(24)] public string ReceiptNo { get; set; }
    public int BillId { get; set; }
    public Bill Bill { get; set; }
    public decimal Amount { get; set; }
    public PaymentMode Mode { get; set; }
    [StringLength(60)] public string Reference { get; set; }
    public DateTime PaidOn { get; set; } = DateTime.Today;
    public int ReceivedById { get; set; }
        public int? RetailShopId { get; set; }
    public RetailShop RetailShop { get; set; }
}

public class Feedback
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }
    public int? ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; }
    [Range(1, 5)] public int Rating { get; set; } = 5;
    [Required, StringLength(600)] public string Comments { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.Now;
}

public class OrderDocument
{
    public int Id { get; set; }
    public int? ServiceOrderId { get; set; }
    public ServiceOrder ServiceOrder { get; set; }
    public int CustomerId { get; set; }
    public DocumentKind Kind { get; set; }
    [Required, StringLength(200)] public string OriginalName { get; set; }
        [Required, StringLength(300)] public string StoredPath { get; set; }
    public DateTime UploadedOn { get; set; } = DateTime.Now;
    public int UploadedById { get; set; }
}

public class SequenceCounter
{
    [Key, StringLength(60)] public string Key { get; set; }
    public long Value { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; }
}
