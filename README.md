# Nexus.Web Student Project Guide

Nexus.Web is an ASP.NET Core MVC application (.NET 8, C#) for a telephone, dial-up and broadband service provider. It handles orders, feasibility checks, connections, postpaid billing, payments, stock and purchasing. The database is SQL Server 2022, accessed with Entity Framework Core 8. Styles come from Bootstrap 5 compiled from `Styles/site.scss`.

## Run the application

You need the .NET 8 SDK and a SQL Server 2022 instance (LocalDB or full edition).

1. Open `appsettings.json` and check `ConnectionStrings:Default`. It points to `localhost` with Windows authentication. Change it if your server is different.
2. From the `Nexus.Web` folder, run:

```
dotnet run
```

On first start the app creates the database `NexusServiceMarketing` and loads plans, rates, cities, shops, employees, customers and demo data.

You only need Node.js and npm if you edit the SCSS styles (`npm install && npm run build:css`).

## Demo logins

All demo accounts use the password `Nexus@2026`.

| Username | Role | What you can do |
|---|---|---|
| `admin` | Admin | employees, plans, rates, stock, vendors, shops, purchasing |
| `accounts1` | Accounts | bills, payments at the city office |
| `tech1`, `tech2` | Technical | feasibility, connections, equipment |
| `retail1`, `retail2` | Retail employee | walk-in orders at Nexus Riverton Central / Nexus Halden Plaza |
| `mokonkwo`, `praghunathan` | Customer | portal, own orders, bills, payments, feedback |

The public tracker at `/Track` lets anyone look up an order id or account id without signing in. It shows status only. It does not show names, phones or addresses.

## IDs used in the project

- Order id: one letter (`D`, `B` or `T`) plus 10 digits, for example `D0000123456`.
- Account id: one letter plus a 3-digit city code plus a 12-digit serial, 16 characters in total.

`D` means dial-up, `B` means broadband, `T` means telephone.

## eProject process overview

eProjects is a different way of learning that adds to your existing course material. Read the MS-Word/PDF eProject specification carefully. It contains the specification you must build.

## Status report submission

During the project tenure you must submit two status reports at equal intervals (every 7th or 10th day, or as your coordinator sets).

Each status report must include:

- Overall progress of the project development.
- Tasks completed and current status.
- Challenges, if any.
- Supporting files: developed source code, database scripts, screenshots, or other relevant documents for progress verification.

Put all files in one folder, ZIP the folder, and submit it through the Query/Status section.

## Final project submission

When the project is complete, prepare one compressed ZIP folder containing every deliverable:

- Complete source code
- Project documentation, including login credentials for the project and database
- Database backup
- Any other relevant project files

Upload the ZIP folder as the final project submission.

Name the ZIP folder so it clearly shows the Batch, Group and Project Title. This makes it easy to identify during evaluation.

Only one submission is allowed per group. Once submitted, no additional or changed files are allowed. Check carefully that all relevant files are included before you submit.

## Team

| Student ID | Name |
|---|---|
| Student1640363 | SOHAIB ALI |
| Student1668133 | YOGESH |
| Student1628719 | MUHAMMAD ABDULLAH |
| Student1661959 | ASHAN ABDUR REHMAN ZAHID |
