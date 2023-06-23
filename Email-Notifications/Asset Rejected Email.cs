using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using Stylelabs.M.Sdk.Contracts.Notifications;
using Stylelabs.M.Sdk.Models.Typed;
using Stylelabs.M.Sdk.Extensions;
using Stylelabs.M.Base.Querying.Linq;
using Stylelabs.M.Sdk.Models.Notifications;
using Stylelabs.M.Framework.Essentials.LoadConfigurations;
using Stylelabs.M.Framework.Essentials.LoadOptions;
using Newtonsoft.Json.Linq;

// Implementation Details
const string RejectedEmailTemplate = "RejectedEmailTemplate";
const string UploadersGroup = "Creators-User-Group";

var entityModified = Context.Target as IEntity;

// Entry point
await ProcessRejectedNotification();

async Task ProcessRejectedNotification() {
    try
    {
        var loadConfiguration = new EntityLoadConfiguration
        {
            CultureLoadOption = new CultureLoadOption("en-US")
        };
        var emailtemplate = await MClient.Notifications.GetMailTemplateEntityAsync(RejectedEmailTemplate, loadConfiguration).ConfigureAwait(false);

        if (emailtemplate == null)
        {
            await CreateAssetRejectedEmailTemplate(MClient).ConfigureAwait(false);
        }
        else
        {
            await SendRejectedEmailNotification();
        }
    }   
    catch(Exception ex)
    {
        await CreateAssetRejectedEmailTemplate(MClient).ConfigureAwait(false);
        await ProcessRejectedNotification();
    }
}

async Task CreateAssetRejectedEmailTemplate(IMClient MClient) {
    MClient.Logger.Info("CreateAssetRejectedEmailTemplate method called");

    // Template variables for uploader
    var templateVariables = new List<TemplateVariable>();
    templateVariables.Add(new TemplateVariable { Name = "AssetTitle", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "DateOfUpload", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "ReasonforReject", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "Logo", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "AssetUrl", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "AssetId", VariableType = TemplateVariableType.Long });
    templateVariables.Add(new TemplateVariable { Name = "AssetCreator", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "Host", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "PublicLink", VariableType = TemplateVariableType.String });

    try
    {
        var entity = await MClient.EntityFactory.CreateAsync(Constants.MailTemplate.DefinitionName, CultureLoadOption.All).ConfigureAwait(false);
        var emailtemplate = MClient.TypedEntityFactory.FromEntity<IMailTemplateEntity>(entity);

        emailtemplate.Name = RejectedEmailTemplate;

        foreach (var culture in entity.Cultures)
        {
            if (culture.DisplayName.Contains("English"))
            {
                emailtemplate.Subject[culture] = "Culture Specific Preferred Subject Line";
                emailtemplate.Description[culture] = "Culture Specific Preferred Description";
                emailtemplate.Body[culture] = "Culture Specific Preferred Body";
                emailtemplate.SetPropertyValue("M.Mailing.TemplateLabel", culture, "Culture Specific Preferred Label");
            }
            //Next Culture which is registered in Content Hub for example I am taking Dutch. 
            //You can optimize this code as per you need as these templates could be later also updated by code or from Content Hub itself.
            if (culture.DisplayName.Contains("Dutch"))
            {
               emailtemplate.Subject[culture] = "Culture Specific Preferred Subject Line";
                emailtemplate.Description[culture] = "Culture Specific Preferred Description";
                emailtemplate.Body[culture] = "Culture Specific Preferred Body";
                emailtemplate.SetPropertyValue("M.Mailing.TemplateLabel", culture, "Culture Specific Preferred Label");
            }
        }

        emailtemplate.SetTemplateVariables(templateVariables);

        await MClient.Entities.SaveAsync(emailtemplate).ConfigureAwait(false);
        MClient.Logger.Info("Email Template created with Name: " + RejectedEmailTemplate);
    }
    catch (Exception ex)
    {
        MClient.Logger.Info(ex.ToString());
    }
}

async Task SendRejectedEmailNotification() {
    // User
    var userIdSpecific = entityModified.CreatedBy;
    var uploader = await MClient.Users.GetUserAsync(userIdSpecific.Value);
    var userName = uploader.GetPropertyValue<string>("Username");

    // Title
    var titleProperty = entityModified.GetPropertyValue<string>("FileName");

    // Date of Upload
    var dateOfUpload = entityModified.CreatedOn.ToString();
    DateTime dt = DateTime.Parse(dateOfUpload);
    var dateOfUploadConverted = dt.ToString("dd/MM/yyyy");

    // Reason for rejection
    var reasonForRejection = entityModified.GetPropertyValue<string>("ReasonForRejection");

    // Host
    var settingsConfig = await MClient.Settings.GetSettingAsync("PortalConfiguration", "Hostname").ConfigureAwait(false); 
    var host = settingsConfig?.GetProperty<ICultureInsensitiveProperty>("M.Setting.Value").GetValue<JObject>().GetValue("HostnameConfiguration").ToString();

    // Uploader EID
    String[] parts = userName.Split(new[]{ '@' });
    String uploaderEID = parts[0];

    // Public link
    var publicLinkId = await CreatePublicLink(long.Parse(entityModified.Id.ToString()), "thumbnail").ConfigureAwait(false);
    var publicLink = await FetchPublicLink(host, publicLinkId).ConfigureAwait(false);

    var usersList = new List<string>();
    usersList.Add(userName);
    var notificationRequest = new MailRequestByUsername
        {
                MailTemplateName = RejectedEmailTemplate,
                Recipients = usersList
        };

    notificationRequest.Variables.Add("AssetTitle", titleProperty);
    notificationRequest.Variables.Add("DateOfUpload", dateOfUploadConverted);
    notificationRequest.Variables.Add("ReasonforReject", reasonForRejection);
    notificationRequest.Variables.Add("Logo", "Logo-Link");
    notificationRequest.Variables.Add("AssetUrl", "Asset URL retrieved");
    notificationRequest.Variables.Add("AssetId", entityModified.Id);
    notificationRequest.Variables.Add("AssetCreator", uploaderEID);
    notificationRequest.Variables.Add("Host", host);
    notificationRequest.Variables.Add("PublicLink", publicLink.ToString());

    await MClient.Notifications.SendEmailNotificationAsync(notificationRequest); 
    MClient.Logger.Info("Asset Reject Notification Email Sent.");
}

async Task<long> CreatePublicLink(long assetId, string renditionName)
{
    // Create new Public Link record
    var publicLink = await MClient.EntityFactory.CreateAsync(Constants.PublicLink.DefinitionName);
    // Tell it which rendition it needs to link to
    publicLink.SetPropertyValue("Resource", renditionName);
    // Connect the link record to the appropriate Asset record
    var assetTopublicLinkRelation = publicLink.GetRelation("AssetToPublicLink", RelationRole.Child);
    assetTopublicLinkRelation.SetIds(new long[] { assetId });
    // Save the new Public Link
    long publicLinkId = await MClient.Entities.SaveAsync(publicLink);
    return publicLinkId;
}

async Task<string> FetchPublicLink(string Host, long publicLinkId)
{
    IEntity result = await MClient.Entities.GetAsync(publicLinkId, EntityLoadConfiguration.Full);
    var relativeUrl = await result.GetPropertyValueAsync<string>("RelativeUrl");
    var versionHash = await result.GetPropertyValueAsync<string>("VersionHash");
    var publicLink = "https://"+Host+"/api/public/content/"+relativeUrl+"?v="+versionHash;
    return publicLink;
}