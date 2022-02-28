using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Storage.Auth;
using Newtonsoft.Json;

class Snippets
{
    [SuppressMessage("ReSharper", "UnusedVariable")]
    void AttachmentSending()
    {
        #region AttachmentSending

        var payload = new MyMessage
        {
            MyProperty = "The Value"
        };
        var serialized = JsonConvert.SerializeObject(payload);
        var payloadAsBytes = Encoding.UTF8.GetBytes(serialized);
        var message = new ServiceBusMessage(payloadAsBytes);

        #endregion
    }

    void Configure_blob_sas_uri_override(string storageConnectionString)
    {
        #region Configure_blob_sas_uri_override

        new AzureStorageAttachmentConfiguration(storageConnectionString)
            .WithBlobSasUri(
                messagePropertyToIdentifySasUri: "mySasUriProperty",
                sasTokenValidationTime: TimeSpan.FromHours(12));

        #endregion
    }

    [SuppressMessage("ReSharper", "UnusedVariable")]
    void AttachmentSendingSas()
    {
        #region AttachmentSendingSas

        var payload = new MyMessage
        {
            MyProperty = "The Value"
        };
        var serialized = JsonConvert.SerializeObject(payload);
        var payloadAsBytes = Encoding.UTF8.GetBytes(serialized);
        var message = new ServiceBusMessage(payloadAsBytes);

        #endregion
    }

    void Configure_criteria_for_message_max_size_identification(string storageConnectionString)
    {
        #region Configure_criteria_for_message_max_size_identification

        // messages with body > 200KB should be converted to use attachments
        new AzureStorageAttachmentConfiguration(storageConnectionString,
            messageMaxSizeReachedCriteria: message => message.Body.ToArray().Length > 200 * 1024);

        #endregion
    }

    [SuppressMessage("ReSharper", "UnusedVariable")]
    void Configuring_connection_string_provider(string connectionString)
    {
        #region Configuring_connection_string_provider

        var provider = new PlainTextConnectionStringProvider(connectionString);
        var config = new AzureStorageAttachmentConfiguration(provider);

        #endregion
    }

    [SuppressMessage("ReSharper", "UnusedVariable")]
    void Configuring_plugin_using_StorageCredentials(string connectionString, string blobEndpoint)
    {
        #region Configuring_plugin_using_StorageCredentials

        var credentials = new StorageCredentials( /*Shared key OR Service SAS OR Container SAS*/);
        var config = new AzureStorageAttachmentConfiguration(credentials, blobEndpoint);

        #endregion
    }

    async Task Upload_attachment_without_registering_plugin(ServiceBusMessage message, AzureStorageAttachmentConfiguration config)
    {
        #region Upload_attachment_without_registering_plugin

        //To make it possible to use SAS URI when downloading, use WithBlobSasUri() when creating configuration object
        await message.UploadAzureStorageAttachment(config);

        #endregion
    }

    async Task Download_attachment_without_registering_plugin(ServiceBusReceivedMessage message, AzureStorageAttachmentConfiguration config)
    {
        #region Download_attachment_without_registering_plugin

        //Using SAS URI with default message property ($attachment.sas.uri)
        await message.DownloadAzureStorageAttachment();

        //Using SAS URI with custom message property
        await message.DownloadAzureStorageAttachment("$custom-attachment.sas.uri");

        //Using configuration object
        await message.DownloadAzureStorageAttachment(config);

        #endregion
    }

    class MyMessage
    {
        public string MyProperty { get; set; } = string.Empty;
    }
}