using System.Net.Http.Headers;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using OfficeOpenXml;
using AuthenticationException = System.Security.Authentication.AuthenticationException;

namespace ListAndDeleteAppRoles
{
public class Program
{
    private static GraphServiceClient _graphClient;
    private static string _accessToken;
    private static readonly string clientId = "<client_Id>";
    private static readonly string tenant = "<tenent_Id>";
    private static readonly string clientSecret = "<clientSecret>";
    private static readonly string object_Id = "<object_Id>";
    private static readonly string azureServicePrinciple = "<azureServicePrinciple>";
    //private static readonly string searchString = "stagingTestAuto";
    private static readonly string searchString = "TestAutoHub";

    private static async Task Main()
    {
        await ListAppRolesBasedOnSearchString();
        await HandleRemoveAzureAppRole();
    }

    private static async Task<string> GetAccessToken()
    {
        var scopes = new List<string> { "https://graph.microsoft.com/.default" };

        var msalClient = ConfidentialClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, tenant)
            .WithClientSecret(clientSecret)
            .Build();

        var userAssertion = new UserAssertion("testDrive", "client_credentials");
        try
        {
            var token = await msalClient.AcquireTokenOnBehalfOf(scopes, userAssertion).ExecuteAsync();
            return token.AccessToken;
        }
        catch
        {
            throw new AuthenticationException("Issue on getting access token");
        }
    }

    private static GraphServiceClient InitiateGraphServiceClient(string accessToken)
    {
        var authenticationProvider = new DelegateAuthenticationProvider(
            requestMessage =>
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return Task.FromResult(0);
            });
        return new GraphServiceClient(authenticationProvider);
    }

    private static async Task<Application> GetAzureApp()
    {
        _accessToken = await GetAccessToken();
        _graphClient = InitiateGraphServiceClient(_accessToken);
        var application = await _graphClient.Applications[object_Id].Request().GetAsync();

        return application;
    }

    public static async Task<bool> ListAppRolesBasedOnSearchString()
    {
        var app = await GetAzureApp();
        var appRoles = app.AppRoles.ToList();

        var countAppRoles = 0;
        var appRolesData = new List<AppRolesData>();
        AppRolesData appRoleData;

        foreach (var appRole in appRoles)
            //if ((bool)appRole?.Description.Contains(searchString))
            {
                countAppRoles++;
                appRoleData = new AppRolesData();
                appRoleData.roleNo = countAppRoles.ToString();
                appRoleData.isEnabled = appRole.IsEnabled.ToString();
                appRoleData.displayName = appRole.DisplayName;
                appRoleData.description = appRole.Description;
                appRoleData.value = appRole.Value;

                /*var listUsers = await GetUsersAssignedToRole(appRole.Id.Value);
                foreach (var user in listUsers)
                {
                    appRoleData.usersAssignedToRole = user + "    ";

                }*/
                appRolesData.Add(appRoleData);
            }

        ExportToExcel(appRolesData);

        return true;
    }

    public static async Task<bool> HandleRemoveAzureAppRole()
    {
        var app = await GetAzureApp();
        var appRoles = app.AppRoles.ToList();

        foreach (var appRole in appRoles)
        {
            if ((bool)appRole?.Description.Contains(searchString))
            {
                var isDisabledAppRole = await DisableAppRole(appRole.Id.Value);
                if (isDisabledAppRole) await DeleteAppRole(appRole.Id.Value);
            }
        }
        return false;
    }

    private static async Task<bool> DisableAppRole(Guid hubId)
        {
            var app = await GetAzureApp();
            var appRoles = app.AppRoles.ToList();
            var modifiedAppRole = appRoles.FirstOrDefault(p => p.Id.Value.Equals(hubId));
            if (modifiedAppRole != null)
            {
                appRoles.Where(p => p.Id == modifiedAppRole.Id).ToList().ForEach(p =>
                {
                    p.Value = $"#HUB_{hubId}";
                    p.IsEnabled = false;
                    p.AllowedMemberTypes = new[] { "User" };
                });

                try
                {
                    app.AppRoles = appRoles;
                    //first save that the appRole is disabled 
                    await _graphClient.Applications[object_Id]
                        .Request()
                        .UpdateAsync(app);

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                    throw;
                }
            }
            return true;
        }

        private static async Task<bool> DeleteAppRole(Guid hubId)
        {
            var app = await GetAzureApp();
            var appRoles = app.AppRoles.ToList();
            var modifiedAppRole = appRoles.FirstOrDefault(p => p.Id.Value.Equals(hubId));

            // remove appRole
            if (modifiedAppRole != null)
            {
                appRoles.Remove(modifiedAppRole);
                app.AppRoles = appRoles;

                await _graphClient.Applications[object_Id]
                    .Request()
                    .UpdateAsync(app);
              
            }
            return true;
        }

        private async Task RemoveAppRoleFromUsers(Guid id)
        {
            var usersAssignedToRole = await GetUsersAssignedToRole(id);
            if (usersAssignedToRole != null)
                foreach (var userAssignment in usersAssignedToRole)
                    await _graphClient.ServicePrincipals[azureServicePrinciple].AppRoleAssignedTo[userAssignment.Id]
                        .Request()
                        .DeleteAsync();
        }

        public static async Task<List<AppRoleAssignment>> GetUsersAssignedToRole(Guid id)
        {
            var appRoleAssignments = new List<AppRoleAssignment>();

            var assignments = await _graphClient.ServicePrincipals[azureServicePrinciple].AppRoleAssignedTo
                .Request()
                .Top(998)
                .GetAsync();

            appRoleAssignments.AddRange(assignments.CurrentPage);
            while (assignments.NextPageRequest != null)
            {
                assignments = await assignments.NextPageRequest.GetAsync();
                appRoleAssignments.AddRange(assignments.CurrentPage);
            }

            var result = appRoleAssignments.Where(x => x.AppRoleId == id).ToList();
            return result;
        }

        private static async Task ExportToExcel(List<AppRolesData> data)
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var package = new ExcelPackage())
                {
                    // Add a worksheet
                    var worksheet = package.Workbook.Worksheets.Add("Sheet1");

                    // Add headers
                    worksheet.Cells["A1"].Value = "RoleNo";
                    worksheet.Cells["B1"].Value = "DisplayName";
                    worksheet.Cells["C1"].Value = "Description";
                    worksheet.Cells["D1"].Value = "IsEnabled";
                    worksheet.Cells["E1"].Value = "Value";
                    worksheet.Cells["F1"].Value = "usersAssignedToRole";

                    // Add data
                    for (var i = 0; i < data.Count; i++)
                    {
                        worksheet.Cells[i + 2, 1].Value = data[i].roleNo;
                        worksheet.Cells[i + 2, 2].Value = data[i].displayName;
                        worksheet.Cells[i + 2, 3].Value = data[i].description;
                        worksheet.Cells[i + 2, 4].Value = data[i].isEnabled;
                        worksheet.Cells[i + 2, 5].Value = data[i].value;
                        worksheet.Cells[i + 2, 6].Value = data[i].usersAssignedToRole;
                    }

                    // Save the file
                    var fileInfo = new FileInfo("output.xlsx");
                    package.SaveAs(fileInfo);
                }
            }
            catch (Exception ex)
            {
            }
        }
    }
public class AppRolesData
    {
        public string roleNo { get; set; }

        public string displayName { get; set; }

        public string description { get; set; }

        public string isEnabled { get; set; }

        public string value { get; set; }

        public string usersAssignedToRole { get; set; }
    }
}