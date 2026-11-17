using System.Text.RegularExpressions;
using Nexus.Web.Models;

namespace Nexus.Web.Services;

public enum IdKind { Unknown, Order, Account }

/// <summary>
/// Order id   : 11 chars  = type letter + 10-digit serial          e.g. D0000000001
/// Account id : 16 chars  = type letter + 3-digit city + 12-digit serial   e.g. B101000000000042
/// </summary>
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

    /// <summary>Accepts messy input (spaces, dashes, lower case) and returns one clean form.</summary>
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

    /// <summary>Display form, chunked so it can be read and copied: D 0000 000001 / B 101 0000 0000 0042.</summary>
    public static string Chunk(string id)
    {
        const char gap = '\u202F'; // narrow no-break space; Normalize() strips it again on paste
        id = Normalize(id);
        return Detect(id) switch
        {
            IdKind.Order => $"{id[0]}{gap}{id.Substring(1, 4)}{gap}{id.Substring(5)}",
            IdKind.Account => $"{id[0]}{gap}{id.Substring(1, 3)}{gap}{id.Substring(4, 4)}{gap}{id.Substring(8, 4)}{gap}{id.Substring(12, 4)}",
            _ => id
        };
    }
}
