using Contract.Services.Interface;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace Services.Infrastructure
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _from;
        private readonly string _password;
        private readonly bool _ssl;

        public SmtpEmailSender(IConfiguration config)
        {
            var smtp = config.GetSection("SmtpSettings");
            _host = smtp["Host"]!;
            _port = int.Parse(smtp["Port"]!);
            _from = smtp["FromEmail"]!;
            _password = smtp["Password"]!;
            _ssl = bool.Parse(smtp["EnableSsl"]!);
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            using var client = new SmtpClient(_host, _port)
            {
                Credentials = new NetworkCredential(_from, _password),
                EnableSsl = _ssl
            };

            var mail = new MailMessage(_from, to, subject, htmlBody)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(mail);
        }
    }

}
