using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

public class DynamicsCrmCallbackService : ICallbackService
{
    private readonly IConfiguration configuration;
    private readonly ILogger<DynamicsCrmCallbackService> logger;

    public DynamicsCrmCallbackService(IConfiguration configuration, ILogger<DynamicsCrmCallbackService> logger)
    {
        this.configuration = configuration;
        this.logger = logger;
    }

    public void CreateCallback(string callSid, string phoneNumber)
    {
        using var serviceClient = CreateCrmServiceClient();
        // find matching contact or create one
        QueryExpression query = new QueryExpression("contact");
        query.PageInfo.Count = 1;
        query.PageInfo.PageNumber = 1;
        query.ColumnSet = new ColumnSet(true);
        query.Criteria.AddCondition("mobilephone", ConditionOperator.Equal, phoneNumber);
        var contacts = serviceClient.RetrieveMultiple(query);

        Entity contact;
        if (contacts.Entities.Count == 1)
        {
            contact = contacts.Entities[0];
        }
        else
        {
            logger.LogWarning("{AmountOfContacts} contacts found with phone number {PhoneNumber}", contacts.Entities.Count, phoneNumber);

            contact = new Entity("contact");
            contact["mobilephone"] = phoneNumber;
            contact.Id = serviceClient.Create(contact);
            logger.LogInformation("Created contact with phone number {PhoneNumber}", phoneNumber);
        }

        // create callback
        var callbackEntity = new Entity("cr196_callback");
        callbackEntity["subject"] = $"Callback requested by {phoneNumber}";
        callbackEntity["cr196_twiliocallsid"] = callSid;
        callbackEntity["cr196_phonenumber"] = phoneNumber;
        callbackEntity["regardingobjectid_contact@odata.bind"] = $"/contacts({contact.Id})";
        var callbackGuid = serviceClient.Create(callbackEntity);
        logger.LogInformation("New Callback created with GUID {CallbackGuid}", callbackGuid);
    }

    public void AddTranscriptToCallback(string callSid, string transcript)
    {
        using var serviceClient = CreateCrmServiceClient();
        QueryExpression query = new QueryExpression("cr196_callback");
        query.PageInfo.Count = 1;
        query.PageInfo.PageNumber = 1;
        query.ColumnSet = new ColumnSet(true);
        query.Criteria.AddCondition("cr196_twiliocallsid", ConditionOperator.Equal, callSid);
        var callbacks = serviceClient.RetrieveMultiple(query);

        if (callbacks.Entities.Count != 1)
        {
            logger.LogError("{AmountOfCallbacks} callbacks returned for CallSid {CallSid}", callbacks.Entities.Count, callSid);
            return;
        }

        var callback = callbacks.Entities[0];
        callback["cr196_transcript"] = transcript;
        serviceClient.Update(callback);

        logger.LogInformation("Callback updated with transcript (Callback GUID {CallbackGuid})", callback.Id);
    }

    public ServiceClient CreateCrmServiceClient()
    {
        string url = configuration["DynamicsCrmUrl"];
        string clientId = configuration["DynamicsCrmClientId"];
        string clientSecret = configuration["DynamicsCrmClientSecret"];
        var connectionString = @$"Url={url};AuthType=ClientSecret;ClientId={clientId};ClientSecret={clientSecret};RequireNewInstance=true";

        return new ServiceClient(connectionString);
    }
}
