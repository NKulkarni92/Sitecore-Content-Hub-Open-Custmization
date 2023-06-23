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
using Stylelabs.M.Base.Querying.Filters;
using Newtonsoft.Json.Linq;

// Implementation Details
const string ApproverEmailTemplate = "ApproverEmailTemplate";
const string CreatorEmailTemplate = "CreatorEmailTemplate";
const string ApproversGroup = "Content-Approvers";

var entityModified = Context.Target as IEntity;

/**
* Shared email template properties
*/
var titleProperty = entityModified.GetPropertyValue<string>("FileName");

// Date of upload
var dateOfUpload = entityModified.CreatedOn.ToString();
DateTime dt = DateTime.Parse(dateOfUpload);
var dateOfUploadConverted = dt.ToString("dd/MM/yyyy");

// Host
var settingsConfig = await MClient.Settings.GetSettingAsync("PortalConfiguration", "Hostname").ConfigureAwait(false); 
var host = settingsConfig?.GetProperty<ICultureInsensitiveProperty>("M.Setting.Value").GetValue<JObject>().GetValue("HostnameConfiguration").ToString();

// Public link
var publicLinkId = await CreatePublicLink(long.Parse(entityModified.Id.ToString()), "thumbnail").ConfigureAwait(false);
var publicLink = await FetchPublicLink(host, publicLinkId).ConfigureAwait(false);

// Entry point
// Send notifications to Approvers
await ProcessApproverNotification();

// Send notification to Creator
await ProcessCreatorNotification();

async Task ProcessApproverNotification() {
    try
    {
        var loadConfiguration = new EntityLoadConfiguration
        {
            CultureLoadOption = new CultureLoadOption("en-US")
        };
        var emailtemplate = await MClient.Notifications.GetMailTemplateEntityAsync(ApproverEmailTemplate, loadConfiguration).ConfigureAwait(false);

        if (emailtemplate == null)
        {
            await CreateAssetSubmissionApproverEmailTemplate(MClient).ConfigureAwait(false);
        }
        else
        {
            await SendApproverEmailNotification();
        }
    }   
    catch(Exception ex)
    {
        await CreateAssetSubmissionApproverEmailTemplate(MClient).ConfigureAwait(false);
        await ProcessApproverNotification();
    }
}

async Task ProcessCreatorNotification() {
    try
    {
        var loadConfiguration = new EntityLoadConfiguration
        {
            CultureLoadOption = new CultureLoadOption("en-US")
        };
        var emailtemplate = await MClient.Notifications.GetMailTemplateEntityAsync(CreatorEmailTemplate, loadConfiguration).ConfigureAwait(false);

        if (emailtemplate == null)
        {
            await CreateAssetSubmissionCreatorEmailTemplate(MClient).ConfigureAwait(false);
        }
        else
        {
            await SendCreatorEmailNotification();
        }
    }   
    catch(Exception ex)
    {
        await CreateAssetSubmissionCreatorEmailTemplate(MClient).ConfigureAwait(false);
        await ProcessCreatorNotification();
    }
}

async Task CreateAssetSubmissionApproverEmailTemplate(IMClient MClient)
{
    MClient.Logger.Info("CreateAssetSubmissionApproverEmailTemplate method called");

    // Template variables for approvers
    var templateVariables = new List<TemplateVariable>();
    templateVariables.Add(new TemplateVariable { Name = "FirstNameOfRecipient", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "AssetTitle", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "DateOfUpload", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "AssetUrl", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "AssetId", VariableType = TemplateVariableType.Long });
    templateVariables.Add(new TemplateVariable { Name = "Logo", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "Host", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "PublicLink", VariableType = TemplateVariableType.String });

    try
    {
        var entity = await MClient.EntityFactory.CreateAsync(Constants.MailTemplate.DefinitionName, CultureLoadOption.All).ConfigureAwait(false);
        var emailtemplate = MClient.TypedEntityFactory.FromEntity<IMailTemplateEntity>(entity);

        emailtemplate.Name = ApproverEmailTemplate;

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
        MClient.Logger.Info("Email Template created with Name: " + ApproverEmailTemplate);
    }
    catch (Exception ex)
    {
        MClient.Logger.Info(ex.ToString());
    }
}

async Task CreateAssetSubmissionCreatorEmailTemplate(IMClient MClient) {
    MClient.Logger.Info("CreateAssetSubmissionCreatorEmailTemplate method called");

    // Template variables for creator
    var templateVariables = new List<TemplateVariable>();
    templateVariables.Add(new TemplateVariable { Name = "AssetTitle", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "AssetDescription", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "DateOfUpload", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "AssetId", VariableType = TemplateVariableType.Long });
    templateVariables.Add(new TemplateVariable { Name = "Logo", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "Host", VariableType = TemplateVariableType.String });
    templateVariables.Add(new TemplateVariable { Name = "PublicLink", VariableType = TemplateVariableType.String });

    try
    {
        var entity = await MClient.EntityFactory.CreateAsync(Constants.MailTemplate.DefinitionName, CultureLoadOption.All).ConfigureAwait(false);
        var emailtemplate = MClient.TypedEntityFactory.FromEntity<IMailTemplateEntity>(entity);

        emailtemplate.Name = CreatorEmailTemplate;

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
        MClient.Logger.Info("Email Template created with Name: " + CreatorEmailTemplate);
    }
    catch (Exception ex)
    {
        MClient.Logger.Info(ex.ToString());
    }
}

async Task SendApproverEmailNotification() {
    var groupId = MClient.Users.GetUserGroupAsync(ApproversGroup).Result.Id;
    var query = new Query
        {
            Filter = new CompositeQueryFilter()
            {
                Children = new QueryFilter[] {
                    new DefinitionQueryFilter{  Name="User"},
                new RelationQueryFilter
                {
                    Relation = "UserGroupToUser",
                    ParentId = groupId
                }
            },
                CombineMethod = CompositeFilterOperator.And
            }
        };

    var users = MClient.Querying.QueryAsync(query, new EntityLoadConfiguration { RelationLoadOption = RelationLoadOption.All}).Result;
    var groupUsers = new List<IEntity>();

    string userFirstNames = null;
    foreach(var user in users.Items)
    {    
        var groupRelation = user.GetRelation<IChildToManyParentsRelation>("UserGroupToUser");
        
        if (groupRelation.Parents.Contains(groupId ?? 0))
        {
            groupUsers.Add(user);
            userFirstNames = string.Join(" ", user);
        }
    }

    var userNames = MClient.Users.GetUsernamesAsync(groupUsers.Select(i => i.Id ?? 0).ToList()).Result?.Select(i => i.Value).ToList();
    var notificationRequest = new MailRequestByUsername
        {
            MailTemplateName = ApproverEmailTemplate,
            Recipients = userNames
        };
    
    foreach (var user in userNames) {
        MClient.Logger.Info("Approver email: " + user);
    }
    
    notificationRequest.Variables.Add("FirstNameOfRecipient", userFirstNames);
    notificationRequest.Variables.Add("AssetTitle", titleProperty);
    notificationRequest.Variables.Add("DateOfUpload", dateOfUploadConverted);
    notificationRequest.Variables.Add("AssetUrl", "https://" + host + "/nl-NL/" + entityModified.Id);
    notificationRequest.Variables.Add("AssetId", entityModified.Id);
    notificationRequest.Variables.Add("Logo", "https://" + host + "/api/gateway/identifier/rD6qQPwl6ke4d-6EfsG8ZQ/preview");
    notificationRequest.Variables.Add("Host", host);
    notificationRequest.Variables.Add("PublicLink", publicLink.ToString());

    await MClient.Notifications.SendEmailNotificationAsync(notificationRequest);
    MClient.Logger.Info("Asset Submission Notification Email Sent for Approver");
}

async Task SendCreatorEmailNotification() {
    // Uploader
    var userIdSpecific = entityModified.CreatedBy;
    var theOneWhoUploaded = await MClient.Users.GetUserAsync(userIdSpecific.Value);
    var userName = theOneWhoUploaded.GetPropertyValue<string>("Username");
    var creator = new List<string>();
    creator.Add(userName);

    // Asset Description
    var nlNL = CultureInfo.GetCultureInfo("nl-NL");
    var assetDescriptions = entityModified.GetPropertyValue<string>("Description", nlNL);
    if(assetDescriptions != null )
            {
                assetDescriptions = Regex.Replace(assetDescriptions, "<.*?>", String.Empty);
            }

    var notificationRequest = new MailRequestByUsername
        {
            MailTemplateName = CreatorEmailTemplate,
            Recipients = creator
        };

    notificationRequest.Variables.Add("AssetTitle", titleProperty);
    notificationRequest.Variables.Add("AssetDescription", assetDescriptions?.ToString());
    notificationRequest.Variables.Add("DateOfUpload", dateOfUploadConverted);
    notificationRequest.Variables.Add("AssetId", entityModified.Id);
    notificationRequest.Variables.Add("Logo", "https://" + host + "/api/gateway/identifier/rD6qQPwl6ke4d-6EfsG8ZQ/preview");
    notificationRequest.Variables.Add("Host", host);
    notificationRequest.Variables.Add("PublicLink", publicLink.ToString());

    await MClient.Notifications.SendEmailNotificationAsync(notificationRequest);
    MClient.Logger.Info("Asset Submission Notification Email Sent for Uploader");
}

public async Task<long> CreatePublicLink(long assetId, string renditionName)
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

public async Task<string> FetchPublicLink(string Host, long publicLinkId)
{
    IEntity result = await MClient.Entities.GetAsync(publicLinkId, EntityLoadConfiguration.Full);
    var relativeUrl = await result.GetPropertyValueAsync<string>("RelativeUrl");
    var versionHash = await result.GetPropertyValueAsync<string>("VersionHash");
    var publicLink = "https://"+Host+"/api/public/content/"+relativeUrl+"?v="+versionHash;
    return publicLink;
}