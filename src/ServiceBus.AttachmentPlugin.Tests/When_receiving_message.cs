namespace ServiceBus.AttachmentPlugin.Tests
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Xunit;

    public class When_receiving_message : IClassFixture<AzureStorageEmulatorFixture>
    {
        [Fact]
        public async Task Should_throw_exception_with_blob_path_for_blob_that_cant_be_found()
        {
            var payload = "payload";
            var message = new ServiceBusMessage(BinaryData.FromString(payload))
            {
                MessageId = Guid.NewGuid().ToString(),
            };

            var sendingPlugin = new AzureStorageAttachment(new AzureStorageAttachmentConfiguration(
                connectionStringProvider: AzureStorageEmulatorFixture.ConnectionStringProvider, containerName: "attachments"));
            await sendingPlugin.BeforeMessageSend(message);

            var receivingPlugin = new AzureStorageAttachment(new AzureStorageAttachmentConfiguration(
                connectionStringProvider: AzureStorageEmulatorFixture.ConnectionStringProvider, containerName: "attachments-wrong-containers"));

            var exception = await Assert.ThrowsAsync<Exception>(() => receivingPlugin.AfterMessageReceive(
                ServiceBusModelFactory.ServiceBusReceivedMessage(message.Body, properties: message.ApplicationProperties)));
            Assert.Contains("attachments-wrong-containers", actualString: exception.Message);
            Assert.Contains(message.ApplicationProperties["$attachment.blob"].ToString(), actualString: exception.Message);
        }
    }
}
