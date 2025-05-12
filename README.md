---
languages:
- csharp
- aspx-csharp
- bicep
page_type: sample
products:
- azure
- aspnet-core
- azure-app-service
- azure-sql-database
- azure-virtual-network
urlFragment: msdocs-app-service-sqldb-dotnetcore
name: Deploy an ASP.NET Core web app with SQL Database in Azure
description: "A sample application you can use to follow along with Tutorial: Deploy an ASP.NET Core and Azure SQL Database app to Azure App Service."
---

# Deploy an ASP.NET Core web app with SQL Database in Azure

This is an ASP.NET Core application that you can use to follow along with the tutorial at 
[Tutorial: Deploy an ASP.NET Core and Azure SQL Database app to Azure App Service](https://learn.microsoft.com/azure/app-service/tutorial-dotnetcore-sqldb-app) or by using the [Azure Developer CLI (azd)](https://learn.microsoft.com/azure/developer/azure-developer-cli/overview) according to the instructions below.

## Run the sample

This project has a [dev container configuration](.devcontainer/), which makes it easier to develop apps locally, deploy them to Azure, and monitor them. The easiest way to run this sample application is inside a GitHub codespace. Follow these steps:

1. Fork this repository to your account. For instructions, see [Fork a repo](https://docs.github.com/get-started/quickstart/fork-a-repo).

1. From the repository root of your fork, select **Code** > **Codespaces** > **+**.

1. In the codespace terminal, run the following commands:

    ```shell
    dotnet ef database update
    dotnet run
    ```

1. When you see the message `Your application running on port 5093 is available.`, click **Open in Browser**.

## Quick deploy

This project is designed to work well with the [Azure Developer CLI](https://learn.microsoft.com/azure/developer/azure-developer-cli/overview), which makes it easier to develop apps locally, deploy them to Azure, and monitor them.

🎥 Watch a deployment of the code in [this screencast](https://www.youtube.com/watch?v=JDlZ4TgPKYc).

In the GitHub codespace:

1. Log in to Azure.

    ```shell
    azd auth login
    ```

1. Provision and deploy all the resources:

    ```shell
    azd up
    ```

    It will prompt you to create a deployment environment name, pick a subscription, and provide a location (like `westeurope`). Then it will provision the resources in your account and deploy the latest code. If you get an error with deployment, changing the location (like to "centralus") can help, as there may be availability constraints for some of the resources.

1. When `azd` has finished deploying, you'll see an endpoint URI in the command output. Visit that URI, and you should see the CRUD app! 🎉 If you see an error, open the Azure Portal from the URL in the command output, navigate to the App Service, select Logstream, and check the logs for any errors.

1. When you've made any changes to the app code, you can just run:

    ```shell
    azd deploy
    ```

## How is database migrations automated?

The [AZD template](infra/resources.bicep) in this repo secures the database in a virtual network through a private endpoint. The web app can access the database through the private endpoint because it's integrated with the virtual network. In this architecture, the simplest way to do database migrations is directly from within the web app itself.

Because the Linux .NET container in App Service doesn't come with the .NET SDK, you cannot run the migrations command `dotnet ef database update` easily. However, you can upload a [self-contained migrations bundle](https://learn.microsoft.com/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#bundles). This repo automates the deployment of the migrations bundle as follows:

- In [azure.yaml](azure.yaml), use the `prepackage` hook to generate a *migrationsbundle* file with `dotnet ef migrations bundle`.
- In the [.csproj](DotNretCoreSqlDb.csproj) file, include the generated *migrationsbundle* file. During the `azd package` stage, *migrationsbundle* will be added to the deploy package.
- In [infra/resources.bicep](infra/resources.bicep), add the `appCommandLine` property to the web app to run the uploaded *migrationsbundle*.

## Getting help

If you're working with this project and running into issues, please post in [Issues](/issues).

```
msdocs-app-service-sqldb-dotnetcore
├─ .config
│  └─ dotnet-tools.json
├─ .devcontainer
│  ├─ Dockerfile
│  ├─ README.md
│  ├─ devcontainer.json
│  ├─ docker-compose.yml
│  └─ mssql
│     ├─ installSQLtools.sh
│     ├─ postCreateCommand.sh
│     └─ setup.sql
├─ ActionTimerFilter.cs
├─ Azure vault api secrets.txt
├─ ChangingDbInstructions.txt
├─ Controllers
│  ├─ AccountManagementController.cs
│  ├─ AssignmentsController.cs
│  ├─ GroupsController.cs
│  ├─ HomeController.cs
│  ├─ LoginController.cs
│  ├─ ScheduleController.cs
│  ├─ StudentManagementController.cs
│  ├─ StudentsController.cs
│  ├─ TodosController.cs
│  ├─ WebhookController.cs
│  └─ WhatsAppController.cs
├─ Data
│  └─ MyDatabaseContext.cs
├─ DevContainerInstructions.txt
├─ DotNetCoreSqlDb.csproj
├─ DotNetCoreSqlDb.sln
├─ Helpers
│  ├─ DropdownOptions.cs
│  ├─ PasswordHelper.cs
│  └─ TimeZoneMapping.cs
├─ Hubs
│  └─ WhatsAppHub.cs
├─ LICENSE.md
├─ Migrations
│  ├─ 20240621154946_InitialCreate.Designer.cs
│  ├─ 20240621154946_InitialCreate.cs
│  ├─ 20250318205151_addstudenttable.Designer.cs
│  ├─ 20250318205151_addstudenttable.cs
│  ├─ 20250318210746_AddStudentTableFix.Designer.cs
│  ├─ 20250318210746_AddStudentTableFix.cs
│  ├─ 20250318212637_addedcoltotodo.Designer.cs
│  ├─ 20250318212637_addedcoltotodo.cs
│  ├─ 20250320040516_changedemailstonotrequired.Designer.cs
│  ├─ 20250320040516_changedemailstonotrequired.cs
│  ├─ 20250320041430_addedsocialmediastostudents.Designer.cs
│  ├─ 20250320041430_addedsocialmediastostudents.cs
│  ├─ 20250320230107_AddContactTable.Designer.cs
│  ├─ 20250320230107_AddContactTable.cs
│  ├─ 20250323232226_AddNotesAndRemoveOngoingFromStudent.Designer.cs
│  ├─ 20250323232226_AddNotesAndRemoveOngoingFromStudent.cs
│  ├─ 20250324041647_AddSourceToStudentModel.Designer.cs
│  ├─ 20250324041647_AddSourceToStudentModel.cs
│  ├─ 20250324051346_AddStudentTimeZone.Designer.cs
│  ├─ 20250324051346_AddStudentTimeZone.cs
│  ├─ 20250325180509_AddUserTable.Designer.cs
│  ├─ 20250325180509_AddUserTable.cs
│  ├─ 20250325191443_AddAccountingGroupToStudentTable.Designer.cs
│  ├─ 20250325191443_AddAccountingGroupToStudentTable.cs
│  ├─ 20250326044832_ChangedIdsToGuid.Designer.cs
│  ├─ 20250326044832_ChangedIdsToGuid.cs
│  ├─ 20250327024451_changedAccountingGroupToNonNullable.Designer.cs
│  ├─ 20250327024451_changedAccountingGroupToNonNullable.cs
│  ├─ 20250327032939_checkmarkvaluesnonnullable.Designer.cs
│  ├─ 20250327032939_checkmarkvaluesnonnullable.cs
│  ├─ 20250331020321_AddStatusToStudent.Designer.cs
│  ├─ 20250331020321_AddStatusToStudent.cs
│  ├─ 20250405220931_ChangedUserToUsername.Designer.cs
│  ├─ 20250405220931_ChangedUserToUsername.cs
│  ├─ 20250405221539_ChangedUserToUsernameV2.Designer.cs
│  ├─ 20250405221539_ChangedUserToUsernameV2.cs
│  ├─ 20250409033514_AddStudentScheduleTables.Designer.cs
│  ├─ 20250409033514_AddStudentScheduleTables.cs
│  ├─ 20250418225048_changedteacheridtonullableinassignments.Designer.cs
│  ├─ 20250418225048_changedteacheridtonullableinassignments.cs
│  ├─ 20250418225933_MakeTeacherNullable.Designer.cs
│  ├─ 20250418225933_MakeTeacherNullable.cs
│  ├─ 20250418231534_groupentryfixinassignments.cs.Designer.cs
│  ├─ 20250418231534_groupentryfixinassignments.cs.cs
│  ├─ 20250419065424_makeassignmentsrefnullableinschedule.Designer.cs
│  ├─ 20250419065424_makeassignmentsrefnullableinschedule.cs
│  ├─ 20250421213718_AddWhatsAppTable.Designer.cs
│  ├─ 20250421213718_AddWhatsAppTable.cs
│  ├─ 20250421232050_addedwassengermessageid.Designer.cs
│  ├─ 20250421232050_addedwassengermessageid.cs
│  ├─ 20250421233054_addednametowhatsapp.Designer.cs
│  ├─ 20250421233054_addednametowhatsapp.cs
│  ├─ 20250423181522_addIsActiveToGroups.Designer.cs
│  ├─ 20250423181522_addIsActiveToGroups.cs
│  ├─ 20250424211354_changedteacherpaytypetostring.Designer.cs
│  ├─ 20250424211354_changedteacherpaytypetostring.cs
│  ├─ 20250424215803_madegroupnullableinassignments.Designer.cs
│  ├─ 20250424215803_madegroupnullableinassignments.cs
│  ├─ 20250424220022_madegroupnullableinassignments2.Designer.cs
│  ├─ 20250424220022_madegroupnullableinassignments2.cs
│  ├─ 20250428200500_AddDeliveryStatusToWhatsApp.Designer.cs
│  ├─ 20250428200500_AddDeliveryStatusToWhatsApp.cs
│  ├─ 20250502025133_AddHashesToUser.Designer.cs
│  ├─ 20250502025133_AddHashesToUser.cs
│  ├─ 20250502060155_addedMustChangePassword.Designer.cs
│  ├─ 20250502060155_addedMustChangePassword.cs
│  ├─ 20250505004911_CreatedApiLog.Designer.cs
│  ├─ 20250505004911_CreatedApiLog.cs
│  ├─ 20250505010825_WassengerApiLogFix.Designer.cs
│  ├─ 20250505010825_WassengerApiLogFix.cs
│  └─ MyDatabaseContextModelSnapshot.cs
├─ Models
│  ├─ Assignments.cs
│  ├─ Contact.cs
│  ├─ ErrorViewModel.cs
│  ├─ Group.cs
│  ├─ Notes.cs
│  ├─ Schedule.cs
│  ├─ Student.cs
│  ├─ StudentGroupComposition.cs
│  ├─ Teacher.cs
│  ├─ Todo.cs
│  ├─ User.cs
│  ├─ WassengerApiLog.cs
│  └─ WhatsAppVoiceMessages.cs
├─ Program.cs
├─ Properties
│  ├─ launchSettings.json
│  ├─ serviceDependencies.json
│  └─ serviceDependencies.local.json
├─ Queries
│  └─ InsertIntoUser
├─ README.md
├─ Services
│  ├─ IEmailSender.cs
│  ├─ IWhatsAppService.cs
│  ├─ MailKitEmailSender.cs
│  └─ WhatsAppService.cs
├─ Settings
│  └─ EmailSettings.cs
├─ Views
│  ├─ AccountManagement
│  │  ├─ ChangeUsername.cshtml
│  │  └─ PasswordReset.cshtml
│  ├─ Assignments
│  │  ├─ Create.cshtml
│  │  ├─ Details.cshtml
│  │  ├─ Edit.cshtml
│  │  ├─ Index.cshtml
│  │  └─ Manage.cshtml
│  ├─ Groups
│  │  ├─ Create.cshtml
│  │  ├─ Edit.cshtml
│  │  └─ Index.cshtml
│  ├─ Home
│  │  ├─ AccessDenied.cshtml
│  │  ├─ Index.cshtml
│  │  └─ Privacy.cshtml
│  ├─ Login
│  │  └─ Index.cshtml
│  ├─ Schedule
│  │  ├─ Create.cshtml
│  │  ├─ Edit.cshtml
│  │  ├─ Index.cshtml
│  │  └─ Manage.cshtml
│  ├─ Shared
│  │  ├─ Error.cshtml
│  │  ├─ _Layout.cshtml
│  │  ├─ _Layout.cshtml.css
│  │  └─ _ValidationScriptsPartial.cshtml
│  ├─ StudentManagement
│  │  ├─ Create.cshtml
│  │  ├─ Delete.cshtml
│  │  ├─ Details.cshtml
│  │  ├─ Edit.cshtml
│  │  └─ Index.cshtml
│  ├─ Students
│  │  ├─ Index.cshtml
│  │  ├─ Schedule.cshtml
│  │  └─ Zoom.cshtml
│  ├─ Todos
│  │  ├─ Create.cshtml
│  │  ├─ Delete.cshtml
│  │  ├─ Details.cshtml
│  │  ├─ Edit.cshtml
│  │  └─ Index.cshtml
│  ├─ WhatsApp
│  │  └─ index.cshtml
│  ├─ _ViewImports.cshtml
│  └─ _ViewStart.cshtml
├─ appsettings.Development.json
├─ appsettings.json
├─ msdocs-app-service-sqldb-dotnetcore
├─ todo.txt
└─ wwwroot
   ├─ css
   │  └─ site.css
   ├─ favicon.ico
   ├─ js
   │  └─ site.js
   └─ lib
      ├─ bootstrap
      │  ├─ LICENSE
      │  └─ dist
      │     ├─ css
      │     │  ├─ bootstrap-grid.css
      │     │  ├─ bootstrap-grid.css.map
      │     │  ├─ bootstrap-grid.min.css
      │     │  ├─ bootstrap-grid.min.css.map
      │     │  ├─ bootstrap-grid.rtl.css
      │     │  ├─ bootstrap-grid.rtl.css.map
      │     │  ├─ bootstrap-grid.rtl.min.css
      │     │  ├─ bootstrap-grid.rtl.min.css.map
      │     │  ├─ bootstrap-reboot.css
      │     │  ├─ bootstrap-reboot.css.map
      │     │  ├─ bootstrap-reboot.min.css
      │     │  ├─ bootstrap-reboot.min.css.map
      │     │  ├─ bootstrap-reboot.rtl.css
      │     │  ├─ bootstrap-reboot.rtl.css.map
      │     │  ├─ bootstrap-reboot.rtl.min.css
      │     │  ├─ bootstrap-reboot.rtl.min.css.map
      │     │  ├─ bootstrap-utilities.css
      │     │  ├─ bootstrap-utilities.css.map
      │     │  ├─ bootstrap-utilities.min.css
      │     │  ├─ bootstrap-utilities.min.css.map
      │     │  ├─ bootstrap-utilities.rtl.css
      │     │  ├─ bootstrap-utilities.rtl.css.map
      │     │  ├─ bootstrap-utilities.rtl.min.css
      │     │  ├─ bootstrap-utilities.rtl.min.css.map
      │     │  ├─ bootstrap.css
      │     │  ├─ bootstrap.css.map
      │     │  ├─ bootstrap.min.css
      │     │  ├─ bootstrap.min.css.map
      │     │  ├─ bootstrap.rtl.css
      │     │  ├─ bootstrap.rtl.css.map
      │     │  ├─ bootstrap.rtl.min.css
      │     │  └─ bootstrap.rtl.min.css.map
      │     └─ js
      │        ├─ bootstrap.bundle.js
      │        ├─ bootstrap.bundle.js.map
      │        ├─ bootstrap.bundle.min.js
      │        ├─ bootstrap.bundle.min.js.map
      │        ├─ bootstrap.esm.js
      │        ├─ bootstrap.esm.js.map
      │        ├─ bootstrap.esm.min.js
      │        ├─ bootstrap.esm.min.js.map
      │        ├─ bootstrap.js
      │        ├─ bootstrap.js.map
      │        ├─ bootstrap.min.js
      │        └─ bootstrap.min.js.map
      ├─ jquery
      │  ├─ LICENSE.txt
      │  └─ dist
      │     ├─ jquery.js
      │     ├─ jquery.min.js
      │     └─ jquery.min.map
      ├─ jquery-validation
      │  ├─ LICENSE.md
      │  └─ dist
      │     ├─ additional-methods.js
      │     ├─ additional-methods.min.js
      │     ├─ jquery.validate.js
      │     └─ jquery.validate.min.js
      └─ jquery-validation-unobtrusive
         ├─ LICENSE.txt
         ├─ jquery.validate.unobtrusive.js
         └─ jquery.validate.unobtrusive.min.js

```