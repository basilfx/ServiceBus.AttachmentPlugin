namespace ServiceBus.AttachmentPlugin
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Microsoft.Azure.Storage;
    using Microsoft.Azure.Storage.Blob;

    class ReceiveOnlyAzureStorageAttachment
    {
        string messagePropertyToIdentifySasUri;

        public ReceiveOnlyAzureStorageAttachment(string messagePropertyToIdentifySasUri)
        {
            this.messagePropertyToIdentifySasUri = messagePropertyToIdentifySasUri;
        }

        public async Task<ServiceBusReceivedMessage> AfterMessageReceive(ServiceBusReceivedMessage message)
        {
            var applicationProperties = message.ApplicationProperties;
            if (!applicationProperties.ContainsKey(messagePropertyToIdentifySasUri))
            {
                return message;
            }

            var blob = new CloudBlockBlob(new Uri(applicationProperties[messagePropertyToIdentifySasUri].ToString()));
            try
            {
                await blob.FetchAttributesAsync().ConfigureAwait(false);
            }
            catch (StorageException exception)
            {
                throw new Exception($"Blob with name '{blob.Name}' under container '{blob.Container.Name}' cannot be found.", exception);
            }
            var fileByteLength = blob.Properties.Length;
            var bytes = new byte[fileByteLength];
            await blob.DownloadToByteArrayAsync(bytes, 0).ConfigureAwait(false);
            message.GetRawAmqpMessage().Body = new Azure.Core.Amqp.AmqpMessageBody(new[] { BinaryData.FromBytes(bytes).ToMemory() });
            return message;
        }
    }
}