# HackPDM
=======

Open Source PDM (Product Data Management) System.

## Why HackPDM?
Because:
 - We don't need PLM, just file management.
 - We want a Linux backend with high flexibility.
 - We want it hosted locally for security and speed.
 - We want to customize and integrate freely without restrictions.
 - We don't like the high price of other proprietary solutions.
 - We don't want to worry about how many licenses we need.

## Description
HackPDM is a lightweight, local-first PDM solution built with C# and .NET 10. It integrates directly with Odoo for backend database functionality via XML-RPC. It manages CAD files, dependencies, versions, and staged commits using a clear, customizable frontend interface built with WinUI. It is specifically designed to keep track of SolidWorks dependencies using the SWDocumentMgr API but can be extended to other file types.

## Use Cases
- **Small to Medium Engineering Firms:** Managing CAD assemblies, parts, and drawings without the overhead of massive PLM suites.
- **Local Workgroups:** Allowing a local team to check in, check out, and version control files seamlessly without complex licensing or mandatory cloud storage.
- **Odoo Users:** Integrating directly with an existing Odoo instance to link files to items and inventory.

## Setup Process
1. Ensure you have the .NET 10 SDK installed.
2. Install the Windows App SDK.
3. Configure your local or remote Odoo instance.
4. Clone the repository and compile using `dotnet build HackPDM.sln -p:EnableWindowsTargeting=true -p:Platform="x64"`.
5. Check out the included `API.md` for understanding how to integrate or interact with HackPDM codematically.
6. The compiled binary will launch the frontend UI where you can set your remote connection string.

## Client Status Codes
The following icons represent the state of a file in the PDM workspace:
- **ro** = remote only ![alt](./HackPDM_CSharp/Icons/StatusIcons/ro.png)
- **lo** = local only ![alt](./HackPDM_CSharp/Icons/StatusIcons/lo.png)
- **nv** = new remote version ![alt](./HackPDM_CSharp/Icons/StatusIcons/nv.png)
- **cm** = checked out to me ![alt](./HackPDM_CSharp/Icons/StatusIcons/cm.png)
- **co** = checked out to other ![alt](./HackPDM_CSharp/Icons/StatusIcons/co.png)
- **if** = ignore filter ![alt](./HackPDM_CSharp/Icons/StatusIcons/if.png)
- **ft** = no remote file type ![alt](./HackPDM_CSharp/Icons/StatusIcons/ft.png)
- **lm** = local modification ![alt](./HackPDM_CSharp/Icons/StatusIcons/lm.png)
- **dt** = deleted ![alt](./HackPDM_CSharp/Icons/StatusIcons/dt.png)
- **ds** = destroyed ![alt](./HackPDM_CSharp/Icons/StatusIcons/ds.png)

## API and Documentation
Extensive documentation describing the classes, methods, and structures used to interface with the PDM engine can be found in `API.md`. It explains the core C# types, models, and UI mapping properties necessary to extend the backend logic.
