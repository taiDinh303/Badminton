using Contract.Services.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Services.Infrastructure
{
    public class SmsSender : ISmsSender
    {
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _fromNumber;
        private readonly ILogger<SmsSender> _logger;

        public SmsSender(
            IConfiguration config,
            ILogger<SmsSender> logger)
        {
            var sms = config.GetSection("SmsSettings");

            _accountSid = sms["AccountSid"]!;
            _authToken = sms["AuthToken"]!;
            _fromNumber = sms["FromNumber"]!;
            _logger = logger;
        }

        // Gửi SMS thật bằng Twilio
        public async Task SendSmsAsync(
            string phoneNumber,
            string message)
        {
            TwilioClient.Init(
                _accountSid,
                _authToken);

            await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(_fromNumber),
                to: new PhoneNumber(phoneNumber));
        }

        // Test không cần SMS thật
        public Task SendTestSmsAsync(
            string phoneNumber,
            string message)
        {
            _logger.LogInformation(
                "SMS TEST → To: {PhoneNumber} | Message: {Message}",
                phoneNumber,
                message);

            return Task.CompletedTask;
        }
    }
}
