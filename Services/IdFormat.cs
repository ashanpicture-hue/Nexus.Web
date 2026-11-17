using System.Text.RegularExpressions;
using Nexus.Web.Models;

namespace Nexus.Web.Services;

public enum IdKind { Unknown, Order, Account }

public static class IdFormat
{
    private static readonly Regex OrderRx = new(@"^[DBT]\d{10}$", RegexOptions.Compiled);
    private static readonly Regex AccountRx = new(@"^[DBT]\d{15}$", RegexOptions.Compiled);

    public static char TypeCode(ConnectionType t) => t switch
    {
        ConnectionType.DialUp => 'D',
        ConnectionType.Broadband => 'B',
        _ => 'T'
    };

    public static ConnectionType? TypeFromCode(char c) => char.ToUpperInvariant(c) switch
    {
        'D' => ConnectionType.DialUp,
        'B' => ConnectionType.Broadband,
        'T' => ConnectionType.Telephone,
        _ => null
    };

    public static string OrderNo(ConnectionType t, long serial) => TypeCode(t) + serial.ToString("D10");

    public static string AccountId(ConnectionType t, string cityCode, long serial) =>
        TypeCode(t) + cityCode + serial.ToString("D12");

public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var chars = raw.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_' && c != '.');
        return new string(chars.ToArray()).ToUpperInvariant();
    }

    public static IdKind Detect(string raw)
    {
        var id = Normalize(raw);
        if (OrderRx.IsMatch(id)) return IdKind.Order;
        if (AccountRx.IsMatch(id)) return IdKind.Account;
        return IdKind.Unknown;
    }

public static string Chunk(string id)
    {
        const char gap = '\u202F';
        id = Normalize(id);
        return Detect(id) switch
        {
            IdKind.Order => $"{id[0]}{gap}{id.Substring(1, 4)}{gap}{id.Substring(5)}",
            IdKind.Account => $"{id[0]}{gap}{id.Substring(1, 3)}{gap}{id.Substring(4, 4)}{gap}{id.Substring(8, 4)}{gap}{id.Substring(12, 4)}",
            _ => id
        };
    }
}
