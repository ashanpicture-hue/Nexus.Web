using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Nexus.Web.Data;
using Nexus.Web.Models;

namespace Nexus.Web.Services;

/// <summary>Reads the signed-in person from the cookie claims.</summary>
public class CurrentUser
{
    private readonly IHttpContextAccessor _accessor;
    public CurrentUser(IHttpContextAccessor accessor) { _accessor = accessor; }

    private ClaimsPrincipal Principal => _accessor.HttpContext?.User;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public int Id => int.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public string Role => Principal?.FindFirstValue(ClaimTypes.Role);
    public string Name => Principal?.Identity?.Name;
    public int? ShopId => int.TryParse(Principal?.FindFirstValue("shop"), out var id) ? id : null;
    public int? CustomerId => int.TryParse(Principal?.FindFirstValue("customer"), out var id) ? id : null;
    public bool IsStaff => Role != null && Role != Roles.Customer;
    public bool Is(params string[] roles) => Role != null && roles.Contains(Role);
}

/// <summary>Password hashing with PBKDF2-SHA256 and a random salt per password.</summary>
public static class PasswordTool
{
    private const int Iterations = 120_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return false;
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>At least 8 characters with a letter and a digit.</summary>
    public static string Check(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return "Password needs at least 8 characters";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "Password needs a letter and a digit";
        return null;
    }
}

/// <summary>Typed access to AppSetting rows with specification defaults.</summary>
public class SettingsService
{
    private readonly AppDbContext _db;
    public SettingsService(AppDbContext db) { _db = db; }

    public async Task<string> GetAsync(string key)
    {
        var row = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
        if (row != null) return row.Value;
        return SettingKeys.Defaults.TryGetValue(key, out var d) ? d : null;
    }

    public async Task<decimal> GetDecimalAsync(string key)
    {
        var v = await GetAsync(key);
        return decimal.TryParse(v, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
    }

    public async Task<int> GetIntAsync(string key) => (int)await GetDecimalAsync(key);

    public async Task SetAsync(string key, string value)
    {
        var row = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (row == null) _db.Settings.Add(new AppSetting { Key = key, Value = value });
        else row.Value = value;
        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Serial numbers for order ids, account ids, bills, receipts and so on.
/// Each call uses its own short-lived context so a counter is committed immediately,
/// and a row-version check makes two simultaneous callers retry instead of sharing a number.
/// </summary>
public class SequenceService
{
    private readonly DbContextOptions<AppDbContext> _options;
    public SequenceService(DbContextOptions<AppDbContext> options) { _options = options; }

    public async Task<long> NextAsync(string key)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var db = new AppDbContext(_options);
            var counter = await db.Sequences.FirstOrDefaultAsync(c => c.Key == key);
            if (counter == null)
            {
                counter = new SequenceCounter { Key = key, Value = 1 };
                db.Sequences.Add(counter);
            }
            else counter.Value++;

            try
            {
                await db.SaveChangesAsync();
                return counter.Value;
            }
            catch (DbUpdateException)
            {
                // Someone else took this number first. Try again with fresh data.
                await Task.Delay(15 * (attempt + 1));
            }
        }
        throw new InvalidOperationException("Could not allocate a serial number for " + key);
    }

    public async Task<string> OrderNoAsync(ConnectionType type) =>
        IdFormat.OrderNo(type, await NextAsync("ORD-" + IdFormat.TypeCode(type)));

    public async Task<string> AccountIdAsync(ConnectionType type, string cityCode) =>
        IdFormat.AccountId(type, cityCode, await NextAsync($"ACC-{IdFormat.TypeCode(type)}-{cityCode}"));

    public async Task<string> BillNoAsync() =>
        $"BL{DateTime.Today:yyMM}-{await NextAsync("BILL-" + DateTime.Today.ToString("yyMM")):D5}";

    public async Task<string> ReceiptNoAsync() => $"RC-{await NextAsync("RECEIPT"):D6}";
    public async Task<string> PurchaseNoAsync() => $"PO-{await NextAsync("PO"):D5}";
    public async Task<string> EmployeeNoAsync() => $"EMP-{await NextAsync("EMP"):D4}";
}

/// <summary>
/// Stores uploaded paperwork under App_Data/filing/{year}/{cityCode}/{orderNo}/ so records
/// are filed by year and area. Files live outside wwwroot and are only served through a
/// controller that checks the caller's rights.
/// </summary>
public class FileStore
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = { ".pdf", ".jpg", ".jpeg", ".png" };
    private readonly string _root;

    public FileStore(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Storage:FilingRoot"] ?? "App_Data/filing";
        _root = Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
    }

    /// <summary>Returns an error message, or null when the file is acceptable.</summary>
    public static async Task<string> ValidateAsync(IFormFile file)
    {
        if (file == null || file.Length == 0) return null;
        if (file.Length > MaxBytes) return $"{file.FileName} is larger than 5 MB";
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext)) return $"{file.FileName}: use a PDF, JPG or PNG file";

        // Check the first bytes so a renamed program cannot slip through.
        var head = new byte[4];
        using (var s = file.OpenReadStream())
        {
            var read = await s.ReadAsync(head, 0, 4);
            if (read < 4) return $"{file.FileName} is not a valid file";
        }
        var ok = ext switch
        {
            ".pdf" => head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46,
            ".png" => head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47,
            _ => head[0] == 0xFF && head[1] == 0xD8
        };
        return ok ? null : $"{file.FileName} does not look like a {ext.TrimStart('.').ToUpperInvariant()} file";
    }

    public async Task<string> SaveAsync(IFormFile file, int year, string cityCode, string orderNo)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relative = Path.Combine(year.ToString(), cityCode, orderNo, Guid.NewGuid().ToString("N") + ext);
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        using var fs = new FileStream(full, FileMode.Create);
        await file.CopyToAsync(fs);
        return relative.Replace('\\', '/');
    }

    /// <summary>Opens a stored file. Returns null if the path escapes the filing root or is missing.</summary>
    public Stream Open(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return null;
        return new FileStream(full, FileMode.Open, FileAccess.Read);
    }

    public static string ContentType(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        _ => "image/jpeg"
    };
}

/// <summary>Limits lists to what the signed-in person may see. Admin, Technical and Accounts see everything.</summary>
public static class Scoping
{
    public static IQueryable<ServiceOrder> ForUser(this IQueryable<ServiceOrder> q, CurrentUser u)
    {
        if (u.Role == Roles.Customer) { var id = u.CustomerId ?? -1; return q.Where(o => o.CustomerId == id); }
        if (u.Role == Roles.Retail) { var shop = u.ShopId ?? -1; return q.Where(o => o.RetailShopId == shop); }
        return q;
    }

    public static IQueryable<Connection> ForUser(this IQueryable<Connection> q, CurrentUser u)
    {
        if (u.Role == Roles.Customer) { var id = u.CustomerId ?? -1; return q.Where(c => c.CustomerId == id); }
        if (u.Role == Roles.Retail) { var shop = u.ShopId ?? -1; return q.Where(c => c.ServiceOrder.RetailShopId == shop); }
        return q;
    }

    public static IQueryable<Bill> ForUser(this IQueryable<Bill> q, CurrentUser u)
    {
        if (u.Role == Roles.Customer) { var id = u.CustomerId ?? -1; return q.Where(b => b.Connection.CustomerId == id); }
        if (u.Role == Roles.Retail) { var shop = u.ShopId ?? -1; return q.Where(b => b.Connection.ServiceOrder.RetailShopId == shop); }
        return q;
    }

    public static IQueryable<Payment> ForUser(this IQueryable<Payment> q, CurrentUser u)
    {
        if (u.Role == Roles.Customer) { var id = u.CustomerId ?? -1; return q.Where(p => p.Bill.Connection.CustomerId == id); }
        if (u.Role == Roles.Retail)
        {
            var shop = u.ShopId ?? -1;
            return q.Where(p => p.RetailShopId == shop || p.Bill.Connection.ServiceOrder.RetailShopId == shop);
        }
        return q;
    }
}
