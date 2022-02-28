namespace ServiceBus.AttachmentPlugin.Tests
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Xunit;

    public class When_sending_message_with_embedded_block_sas_uri : IClassFixture<AzureStorageEmulatorFixture>
    {
        [Fact]
        public async Task Should_set_sas_uri_when_specified()
        {
            var payload = "payload";
            var bytes = Encoding.UTF8.GetBytes(payload);
            var message = new ServiceBusMessage(bytes)
            {
                MessageId = Guid.NewGuid().ToString(),
            };
            var plugin = new AzureStorageAttachment(new AzureStorageAttachmentConfiguration(
                connectionStringProvider: AzureStorageEmulatorFixture.ConnectionStringProvider, containerName: "attachments", messagePropertyToIdentifyAttachmentBlob: "attachment-id")
                .WithBlobSasUri(sasTokenValidationTime: TimeSpan.FromHours(4), messagePropertyToIdentifySasUri: "mySasUriProperty"));
            var result = await plugin.BeforeMessageSend(message);

            Assert.Equal(new byte[0], result.Body.ToArray());
            Assert.True(message.ApplicationProperties.ContainsKey("attachment-id"));
            Assert.True(message.ApplicationProperties.ContainsKey("mySasUriProperty"));
        }
    }
}
