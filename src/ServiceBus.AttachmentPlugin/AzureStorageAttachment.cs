namespace ServiceBus.AttachmentPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Microsoft.Azure.Storage;
    using Microsoft.Azure.Storage.Blob;

    class AzureStorageAttachment
    {
        internal const string MessageId = "_MessageId";
        internal const string ValidUntilUtc = "_ValidUntilUtc";
        internal const string DateFormat = "yyyy-MM-dd HH:mm:ss:ffffff Z";

        AzureStorageAttachmentConfiguration configuration;

        public AzureStorageAttachment(AzureStorageAttachmentConfiguration configuration)
        {
            Guard.AgainstNull(nameof(configuration), configuration);
            this.configuration = configuration;
        }

        internal static Func<DateTime> DateTimeFunc = () => DateTime.UtcNow;

        public async Task<ServiceBusMessage> BeforeMessageSend(ServiceBusMessage message)
        {
            if (AttachmentBlobAssociated(message.ApplicationProperties))
            {
                return message;
            }

            if (!configuration.MessageMaxSizeReachedCriteria(message))
            {
                return message;
            }

            var containerUri = new Uri($"{configuration.BlobEndpoint}{configuration.ContainerName}");
            var container = new CloudBlobContainer(containerUri, configuration.StorageCredentials);

            try
            {
                // Will only work for Shared Key or Account SAS. For Container SAS will throw an exception.
                if (! await container.ExistsAsync().ConfigureAwait(false))
                {
                    await container.CreateIfNotExistsAsync().ConfigureAwait(false);
                }
            }
            catch (StorageException)
            {
                // swallow in case a container SAS is used
            }

            var blobName = configuration.BlobNameResolver(message);
            var blobUri = new Uri($"{containerUri}/{blobName}");
            var blob = new CloudBlockBlob(blobUri, configuration.StorageCredentials);

            SetValidMessageId(blob, message.MessageId);
            SetValidUntil(blob, message.TimeToLive);

            var bytes = message.Body.ToArray();
            var newBytes = configuration.BodyReplacer(message) ?? new byte[0];

            await blob.UploadFromByteArrayAsync(bytes, 0, bytes.Length).ConfigureAwait(false);

            message.GetRawAmqpMessage().Body = new Azure.Core.Amqp.AmqpMessageBody(new[] { BinaryData.FromBytes(newBytes).ToMemory() });
            message.ApplicationProperties[configuration.MessagePropertyToIdentifyAttachmentBlob] = blob.Name;

            if (!configuration.BlobSasTokenValidationTime.HasValue)
            {
                return message;
            }

            // TODO: only possible if connection string is used
            // configuration.StorageCredentials.IsSharedKey
            var sasUri = TokenGenerator.GetBlobSasUri(blob, configuration.BlobSasTokenValidationTime.Value);
            message.ApplicationProperties[configuration.MessagePropertyForBlobSasUri] = sasUri;
            return message;
        }

        bool AttachmentBlobAssociated(IDictionary<string, object> messageapplicationProperties) =>
            messageapplicationProperties.TryGetValue(configuration.MessagePropertyToIdentifyAttachmentBlob, out var _);

        static void SetValidMessageId(ICloudBlob blob, string messageId)
        {
            if (!string.IsNullOrWhiteSpace(messageId))
            {
                blob.Metadata[MessageId] = messageId;
            }
        }

        static void SetValidUntil(ICloudBlob blob, TimeSpan timeToBeReceived)
        {
            if (timeToBeReceived == TimeSpan.MaxValue)
            {
                return;
            }

            var validUntil = DateTimeFunc().Add(timeToBeReceived);
            blob.Metadata[ValidUntilUtc] = validUntil.ToString(DateFormat);
        }

        public async Task<ServiceBusReceivedMessage> AfterMessageReceive(ServiceBusReceivedMessage message)
        {
            var applicationProperties = message.ApplicationProperties;

            if (!applicationProperties.TryGetValue(configuration.MessagePropertyToIdentifyAttachmentBlob, out var blobNameObject))
            {
                return message;
            }

            var blob = BuildBlob(applicationProperties, blobNameObject);

            try
            {
                await blob.FetchAttributesAsync().ConfigureAwait(false);
            }
            catch (StorageException exception)
            {
                throw new Exception($"Blob with name '{blob.Name}' under container '{blob.Container.Name}' cannot be found."
                    + $" Check {nameof(AzureStorageAttachmentConfiguration)}.{nameof(AzureStorageAttachmentConfiguration.ContainerName)} or"
                    + $" {nameof(AzureStorageAttachmentConfiguration)}.{nameof(AzureStorageAttachmentConfiguration.MessagePropertyToIdentifyAttachmentBlob)} for correct values.", exception);
            }
            var fileByteLength = blob.Properties.Length;
            var bytes = new byte[fileByteLength];
            await blob.DownloadToByteArrayAsync(bytes, 0).ConfigureAwait(false);
            message.GetRawAmqpMessage().Body = new Azure.Core.Amqp.AmqpMessageBody(new[] { BinaryData.FromBytes(bytes).ToMemory() });
            return message;
        }

        CloudBlockBlob BuildBlob(IReadOnlyDictionary<string, object> applicationProperties, object blobNameObject)
        {
            if (configuration.MessagePropertyForBlobSasUri != null)
            {
                if (applicationProperties.TryGetValue(configuration.MessagePropertyForBlobSasUri, out var propertyForBlobSasUri))
                {
                    return new CloudBlockBlob(new Uri((string)propertyForBlobSasUri));
                }
            }

            var blobName = (string) blobNameObject;
            var blobUri = new Uri($"{configuration.BlobEndpoint}{configuration.ContainerName}/{blobName}");
            return new CloudBlockBlob(blobUri, configuration.StorageCredentials);
        }
    }
}
