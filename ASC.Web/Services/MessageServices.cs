using ASC.Web.Configuration;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASC.Web.Services
{
    // This class is used by the application to send Email and SMS
    // when you turn on two-factor authentication in ASP.NET Identity.
    // For more details see this link https://go.microsoft.com/fwlink/?LinkID=532713
    public class AuthMessageSender : IEmailSender, ISmsSender
    {
        private IOptions<ApplicationSettings> _settings;

        public AuthMessageSender(IOptions<ApplicationSettings> settings)
        {
            this._settings = settings;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            MimeMessage emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(this._settings.Value.SMTPAccount));
            emailMessage.To.Add(new MailboxAddress(email));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart("plain")
            {
                Text = message,
            };

            using(SmtpClient client = new SmtpClient())
            {
                await client.ConnectAsync(this._settings.Value.SMTPServer, this._settings.Value.SMTPPort, false);
                await client.AuthenticateAsync(this._settings.Value.SMTPAccount, this._settings.Value.SMTPPassword);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
            }
        }

        public Task SendSmsAsync(string number, string message)
        {
            // Plug in your SMS service here to send a text message.
            return Task.FromResult(0);
        }
    }
}
