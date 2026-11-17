using Microsoft.EntityFrameworkCore;
using Nexus.Web.Models;
using Nexus.Web.Services;

namespace Nexus.Web.Data;

public static class DbSeeder
{
    public const string DemoPassword = "Nexus@2026";

    public static async Task RunAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        if (await db.Users.AnyAsync()) return;

        try
        {
            await SeedReferenceDataAsync(db);
            await SeedPeopleAsync(db);
            await SeedActivityAsync(sp, db);
            log.LogInformation("Starter data loaded");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Starter data could not be loaded completely");
        }
    }

    private static async Task SeedReferenceDataAsync(AppDbContext db)
    {
        db.Cities.AddRange(
            new City { Name = "Riverton", Code = "101" },
            new City { Name = "Halden", Code = "102" },
            new City { Name = "Eastport", Code = "103" },
            new City { Name = "Marlow", Code = "104" });

        foreach (var kv in SettingKeys.Defaults) db.Settings.Add(new AppSetting { Key = kv.Key, Value = kv.Value });

        db.DepositRates.AddRange(
            new SecurityDepositRate { ConnectionType = ConnectionType.DialUp, Amount = 325 },
            new SecurityDepositRate { ConnectionType = ConnectionType.Broadband, Amount = 500 },
            new SecurityDepositRate { ConnectionType = ConnectionType.Telephone, Amount = 250 });

        db.DiscountSchemes.AddRange(
            new DiscountScheme { Name = "Small corporate", MinConnections = 10, MaxConnections = 14, Percent = 25 },
            new DiscountScheme { Name = "Medium corporate", MinConnections = 15, MaxConnections = 24, Percent = 50 },
            new DiscountScheme { Name = "Large corporate", MinConnections = 25, MaxConnections = 50, Percent = 75 },
            new DiscountScheme { Name = "Enterprise", MinConnections = 51, MaxConnections = null, Percent = 100 });

        Plan P(string name, ConnectionType t, PlanCategory c, int? kbps, int? hours, int months, decimal price,
               decimal? local = null, decimal? std = null, decimal? msg = null, string note = null) => new()
        {
            Name = name, ConnectionType = t, Category = c, SpeedKbps = kbps, Hours = hours, ValidityMonths = months,
            Price = price, LocalRate = local, StdRate = std, MobileMsgRate = msg, Description = note
        };

        const ConnectionType D = ConnectionType.DialUp, B = ConnectionType.Broadband, T = ConnectionType.Telephone;
        db.Plans.AddRange(
            P("Dial-up 10 hours", D, PlanCategory.Hourly, null, 10, 1, 50),
            P("Dial-up 30 hours", D, PlanCategory.Hourly, null, 30, 3, 130),
            P("Dial-up 60 hours", D, PlanCategory.Hourly, null, 60, 6, 260),
            P("Dial-up unlimited 28 Kbps, monthly", D, PlanCategory.Unlimited, 28, null, 1, 75),
            P("Dial-up unlimited 28 Kbps, quarterly", D, PlanCategory.Unlimited, 28, null, 3, 150),
            P("Dial-up unlimited 56 Kbps, monthly", D, PlanCategory.Unlimited, 56, null, 1, 100),
            P("Dial-up unlimited 56 Kbps, quarterly", D, PlanCategory.Unlimited, 56, null, 3, 180),
            P("Broadband 30 hours", B, PlanCategory.Hourly, null, 30, 1, 175),
            P("Broadband 60 hours", B, PlanCategory.Hourly, null, 60, 6, 315),
            P("Broadband unlimited 64 Kbps, monthly", B, PlanCategory.Unlimited, 64, null, 1, 225),
            P("Broadband unlimited 64 Kbps, quarterly", B, PlanCategory.Unlimited, 64, null, 3, 400),
            P("Broadband unlimited 128 Kbps, monthly", B, PlanCategory.Unlimited, 128, null, 1, 350),
            P("Broadband unlimited 128 Kbps, quarterly", B, PlanCategory.Unlimited, 128, null, 3, 445),
            P("Local unlimited, yearly", T, PlanCategory.LocalLandline, null, null, 12, 75, 0.55m, null, null, "Local calls only"),
            P("Local, monthly", T, PlanCategory.LocalLandline, null, null, 1, 35, 0.75m, null, null, "Local calls only"),
            P("STD, monthly", T, PlanCategory.StdLandline, null, null, 1, 125, 0.70m, 2.25m, 1.00m, "Local, STD and mobile messaging"),
            P("STD, half-yearly", T, PlanCategory.StdLandline, null, null, 6, 420, 0.60m, 2.00m, 1.15m, "Local, STD and mobile messaging"),
            P("STD, yearly", T, PlanCategory.StdLandline, null, null, 12, 720, 0.60m, 1.75m, 1.25m, "Local, STD and mobile messaging. Price assumed: confirm on the Plans page"));

        var vendors = new[]
        {
            new Vendor { Name = "Tessel Networks", ContactPerson = "Aldo Brandt", Phone = "0215550142", Email = "orders@tessel.example", Address = "14 Foundry Road, Riverton" },
            new Vendor { Name = "Corvane Telecom Supply", ContactPerson = "Leila Haddad", Phone = "0215550187", Email = "sales@corvane.example", Address = "Unit 6, Kiln Park, Halden" }
        };
        db.Vendors.AddRange(vendors);
        await db.SaveChangesAsync();

        db.Products.AddRange(
            new Product { Name = "V.90 external modem", Category = ProductCategory.Modem, Model = "TX-56E", VendorId = vendors[0].Id, ReplacementCharge = 45, StockQty = 60, ReorderLevel = 15, Description = "56 Kbps dial-up modem" },
            new Product { Name = "ADSL modem", Category = ProductCategory.Modem, Model = "AD-2200", VendorId = vendors[0].Id, ReplacementCharge = 80, StockQty = 14, ReorderLevel = 15, Description = "Broadband modem" },
            new Product { Name = "Wireless router", Category = ProductCategory.Router, Model = "WR-300", VendorId = vendors[1].Id, ReplacementCharge = 95, StockQty = 40, ReorderLevel = 10, Description = "Broadband router with four ports" },
            new Product { Name = "Desk telephone", Category = ProductCategory.Telephone, Model = "DP-11", VendorId = vendors[1].Id, ReplacementCharge = 25, StockQty = 80, ReorderLevel = 20, Description = "Corded landline set" });

        db.RetailShops.AddRange(
            new RetailShop { Name = "Nexus Riverton Central", CityId = (await db.Cities.FirstAsync(c => c.Code == "101")).Id, Address = "22 Market Street, Riverton", Phone = "0215550101" },
            new RetailShop { Name = "Nexus Halden Plaza", CityId = (await db.Cities.FirstAsync(c => c.Code == "102")).Id, Address = "Shop 9, Plaza Arcade, Halden", Phone = "0225550102" },
            new RetailShop { Name = "Nexus Eastport Quay", CityId = (await db.Cities.FirstAsync(c => c.Code == "103")).Id, Address = "5 Quay Lane, Eastport", Phone = "0235550103" });
        await db.SaveChangesAsync();
    }

    private static AppUser NewUser(string username, string role, string name, string email, string phone) => new()
    {
        Username = username, PasswordHash = PasswordTool.Hash(DemoPassword), Role = role,
        FullName = name, Email = email, Phone = phone
    };

    private static async Task SeedPeopleAsync(AppDbContext db)
    {
        var shops = await db.RetailShops.OrderBy(s => s.Id).ToListAsync();
        var staff = new (AppUser User, int? Shop)[]
        {
            (NewUser("admin", Roles.Admin, "Naveen Iyer", "naveen.iyer@nexus.example", "0215550001"), null),
            (NewUser("accounts1", Roles.Accounts, "Farah Siddiqui", "farah.siddiqui@nexus.example", "0215550002"), null),
            (NewUser("tech1", Roles.Technical, "Kofi Mensah", "kofi.mensah@nexus.example", "0215550003"), null),
            (NewUser("tech2", Roles.Technical, "Anneli Roos", "anneli.roos@nexus.example", "0215550004"), null),
            (NewUser("retail1", Roles.Retail, "Beatriz Lowe", "beatriz.lowe@nexus.example", "0215550005"), shops[0].Id),
            (NewUser("retail2", Roles.Retail, "Dmitri Pavlov", "dmitri.pavlov@nexus.example", "0225550006"), shops[1].Id)
        };
        var n = 1;
        foreach (var (user, shop) in staff)
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.Employees.Add(new Employee
            {
                UserId = user.Id, EmployeeNo = $"EMP-{n++:D4}", RetailShopId = shop,
                Address = "Staff record", HireDate = new DateTime(2024, 3, 1).AddDays(n * 11)
            });
        }
        var seq = await db.Sequences.FirstOrDefaultAsync(s => s.Key == "EMP");
        if (seq == null) db.Sequences.Add(new SequenceCounter { Key = "EMP", Value = n - 1 });
        await db.SaveChangesAsync();

        var cities = await db.Cities.ToDictionaryAsync(c => c.Code, c => c.Id);
        var marta = NewUser("mokonkwo", Roles.Customer, "Marta Okonkwo", "marta.okonkwo@mail.example", "0215550311");
        var priya = NewUser("praghunathan", Roles.Customer, "Priya Raghunathan", "priya.r@mail.example", "0225550322");
        db.Users.AddRange(marta, priya);
        await db.SaveChangesAsync();

        db.Customers.AddRange(
            new Customer { UserId = marta.Id, FullName = "Marta Okonkwo", Email = marta.Email, Phone = marta.Phone, Address = "41 Alder Court, Riverton", CityId = cities["101"], IdProofType = "Passport", IdProofNumber = "P4471820" },
            new Customer { UserId = priya.Id, FullName = "Priya Raghunathan", Email = priya.Email, Phone = priya.Phone, Address = "8 Weaver Row, Halden", CityId = cities["102"], IdProofType = "Driving licence", IdProofNumber = "DL-90817" },
            new Customer { FullName = "Tomasz Wieczorek", Email = "t.wieczorek@mail.example", Phone = "0215550333", Address = "17 Canal Street, Riverton", CityId = cities["101"], IdProofType = "National ID", IdProofNumber = "N-220918" },
            new Customer { FullName = "Declan Hartley", Kind = CustomerKind.Corporate, CompanyName = "Hartley & Voss Accounting", Email = "accounts@hartleyvoss.example", Phone = "0225550344", Address = "3rd Floor, Plaza Tower, Halden", CityId = cities["102"], IdProofType = "Company registration", IdProofNumber = "CR-55120" },
            new Customer { FullName = "Ines Calderón", Email = "ines.calderon@mail.example", Phone = "0235550355", Address = "Hillcrest Farm Road, Eastport", CityId = cities["103"], IdProofType = "National ID", IdProofNumber = "N-310442" });
        await db.SaveChangesAsync();
    }

    private static async Task SeedActivityAsync(IServiceProvider sp, AppDbContext db)
    {
        var orders = sp.GetRequiredService<OrderService>();
        var conns = sp.GetRequiredService<ConnectionService>();
        var billing = sp.GetRequiredService<BillingService>();

        int Uid(string name) => db.Users.First(u => u.Username == name).Id;
        int Cust(string name) => db.Customers.First(c => c.FullName == name).Id;
        int Plan(string name) => db.Plans.First(p => p.Name == name).Id;
        int Prod(string name) => db.Products.First(p => p.Name == name).Id;
        int City(string code) => db.Cities.First(c => c.Code == code).Id;
        int Shop(string name) => db.RetailShops.First(s => s.Name == name).Id;

        var tech = Uid("tech1");
        var acct = Uid("accounts1");
        var retail1 = Uid("retail1");
        var retail2 = Uid("retail2");

        async Task<ServiceOrder> Place(string customer, ConnectionType t, string plan, string product, int qty, string cityCode,
            string address, string shop, int by, bool existingLandline = false, string landlineAcc = null, string landlinePlan = null)
        {
            var r = await orders.PlaceAsync(new PlaceOrderRequest
            {
                CustomerId = Cust(customer), ConnectionType = t, PlanId = Plan(plan),
                ProductId = product == null ? null : Prod(product), Quantity = qty,
                HasExistingLandline = existingLandline, ExistingLandlineAccountId = landlineAcc,
                LandlinePlanId = landlinePlan == null ? null : Plan(landlinePlan),
                InstallCityId = City(cityCode), InstallAddress = address,
                Channel = shop == null ? OrderChannel.Online : OrderChannel.RetailShop,
                RetailShopId = shop == null ? null : Shop(shop), PlacedById = by
            });
            if (!r.Ok) throw new InvalidOperationException(string.Join("; ", r.Errors));
            return r.Order;
        }

        var o1 = await Place("Marta Okonkwo", ConnectionType.Broadband, "Broadband unlimited 128 Kbps, monthly", "ADSL modem", 1, "101", "41 Alder Court, Riverton", "Nexus Riverton Central", retail1);
        await orders.RecordFeasibilityAsync(o1.Id, FeasibilitySubject.Internet, 3.2m, true, "Cabinet 14", tech);
        var (c1, _) = await conns.ProvisionAsync(o1.Id, null, null, "TX-ADSL-88120", tech);
        var bill1 = (await billing.GenerateAsync(new GenerateBillRequest { ConnectionId = c1[0].Id, PeriodStart = DateTime.Today }, acct)).Bill;
        await billing.PayAsync(bill1.Id, 400m, PaymentMode.Card, "Auth 771204", DateTime.Today, retail1, Shop("Nexus Riverton Central"));

        var o2 = await Place("Tomasz Wieczorek", ConnectionType.Telephone, "STD, monthly", "Desk telephone", 1, "101", "17 Canal Street, Riverton", "Nexus Riverton Central", retail1);
        await orders.RecordFeasibilityAsync(o2.Id, FeasibilitySubject.Landline, 1.9m, true, null, tech);
        var (c2, _) = await conns.ProvisionAsync(o2.Id, null, null, null, tech);
        var bill2 = (await billing.GenerateAsync(new GenerateBillRequest { ConnectionId = c2[0].Id, PeriodStart = DateTime.Today.AddMonths(-1), LocalMinutes = 120, StdMinutes = 40, MobileMsgMinutes = 6 }, acct)).Bill;
        await billing.PayAsync(bill2.Id, bill2.Total, PaymentMode.Cash, null, DateTime.Today, acct, null);

        var o3 = await Place("Priya Raghunathan", ConnectionType.DialUp, "Dial-up unlimited 56 Kbps, monthly", "V.90 external modem", 1, "102", "8 Weaver Row, Halden", null, Uid("praghunathan"), landlinePlan: "Local, monthly");
        await orders.RecordFeasibilityAsync(o3.Id, FeasibilitySubject.Internet, 4.1m, true, null, tech);
        await orders.RecordFeasibilityAsync(o3.Id, FeasibilitySubject.Landline, 2.8m, true, null, tech);
        var (c3, _) = await conns.ProvisionAsync(o3.Id, null, Prod("Desk telephone"), null, tech);
        foreach (var c in c3)
        {
            var b = (await billing.GenerateAsync(new GenerateBillRequest { ConnectionId = c.Id, PeriodStart = DateTime.Today.AddMonths(-1), LocalMinutes = 55 }, acct)).Bill;
            if (b != null) { b.DueDate = DateTime.Today.AddDays(-9); }
        }
        await db.SaveChangesAsync();

        var o4 = await Place("Declan Hartley", ConnectionType.Broadband, "Broadband unlimited 64 Kbps, monthly", "Wireless router", 12, "102", "3rd Floor, Plaza Tower, Halden", "Nexus Halden Plaza", retail2);
        await orders.RecordFeasibilityAsync(o4.Id, FeasibilitySubject.Internet, 1.2m, true, "Fibre riser in building", tech);
        var (c4, _) = await conns.ProvisionAsync(o4.Id, null, null, null, tech);
        await billing.GenerateAsync(new GenerateBillRequest { ConnectionId = c4[0].Id, PeriodStart = DateTime.Today }, acct);

        var o5 = await Place("Ines Calderón", ConnectionType.Telephone, "Local, monthly", "Desk telephone", 1, "103", "Hillcrest Farm Road, Eastport", "Nexus Eastport Quay", retail2);
        await orders.RecordFeasibilityAsync(o5.Id, FeasibilitySubject.Landline, 7.6m, true, "Measured along the road", tech);

        var o6 = await Place("Marta Okonkwo", ConnectionType.Telephone, "Local unlimited, yearly", "Desk telephone", 1, "101", "41 Alder Court, Riverton", null, Uid("mokonkwo"));
        await orders.RecordFeasibilityAsync(o6.Id, FeasibilitySubject.Landline, 3.2m, true, null, tech);
        var o7 = await Place("Tomasz Wieczorek", ConnectionType.DialUp, "Dial-up 30 hours", "V.90 external modem", 1, "101", "17 Canal Street, Riverton", "Nexus Riverton Central", retail1, existingLandline: true, landlineAcc: c2[0].AccountId);
        await orders.StartCheckAsync(o7.Id);
        await Place("Declan Hartley", ConnectionType.Broadband, "Broadband 30 hours", "ADSL modem", 1, "102", "Annexe, Plaza Tower, Halden", "Nexus Halden Plaza", retail2);

        db.Feedbacks.AddRange(
            new Feedback { CustomerId = Cust("Marta Okonkwo"), ServiceOrderId = o1.Id, Rating = 5, Comments = "Technician arrived within two days and the line was live before he left." },
            new Feedback { CustomerId = Cust("Priya Raghunathan"), ServiceOrderId = o3.Id, Rating = 4, Comments = "Both lines worked. The first bill was clear, though I wish the deposit was explained at the shop." });
        await db.SaveChangesAsync();
    }
}
