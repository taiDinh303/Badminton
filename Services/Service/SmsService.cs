using Contract.Services.Interface;
using Microsoft.Extensions.Configuration;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Services.Service
{
    public class SmsService : ISmsService
    {
        private readonly IConfiguration _configuration;

        public SmsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendVerificationCodeAsync(
            string phoneNumber,
            string code)
        {
            var accountSid =
                _configuration["TwilioSettings:AccountSid"];

            var authToken =
                _configuration["TwilioSettings:AuthToken"];

            var messagingServiceSid =
                _configuration["TwilioSettings:MessagingServiceSid"];

            if (phoneNumber.StartsWith("0"))
                phoneNumber = "+84" + phoneNumber.Substring(1);

            TwilioClient.Init(accountSid, authToken);

            await MessageResource.CreateAsync(
                body: $"Badminton Booking: Mã xác thực của bạn là {code}. Mã có hiệu lực trong 5 phút.",
                messagingServiceSid: messagingServiceSid,
                to: new PhoneNumber(phoneNumber));
        }
    }
}
