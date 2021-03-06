﻿using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Umbrella.Utilities.Email.Abstractions;
using Umbrella.Utilities.Email.Options;
using Umbrella.Utilities.Exceptions;
using Umbrella.Utilities.Extensions;

namespace Umbrella.Utilities.Email
{
	/// <summary>
	/// An implementation of the <see cref="IEmailSender" /> abstraction that either sends emails
	/// using a configured SMTP server or saves them to a folder on disk as .eml files. Configuration is specified using
	/// the singleton <see cref="EmailSenderOptions" /> configured with the application's DI container.
	/// </summary>
	public class EmailSender : IEmailSender
	{
		private readonly ILogger<EmailSender> _logger;
		private readonly EmailSenderOptions _options;

		/// <summary>
		/// Create a new instance.
		/// </summary>
		/// <param name="logger">The logger.</param>
		/// <param name="options">The options.</param>
		public EmailSender(
			ILogger<EmailSender> logger,
			EmailSenderOptions options)
		{
			_logger = logger;
			_options = options;
		}

		/// <inheritdoc />
		public async Task SendEmailAsync(string email, string subject, string body, CancellationToken cancellationToken = default, string fromAddress = null)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Guard.ArgumentNotNullOrWhiteSpace(email, nameof(email));
			Guard.ArgumentNotNullOrWhiteSpace(subject, nameof(subject));
			Guard.ArgumentNotNullOrWhiteSpace(body, nameof(body));

			// TODO: Alter the validation inside here to be moved into the options class.
			try
			{
				using var client = new SmtpClient();

				switch (_options.DeliveryMethod)
				{
					case SmtpDeliveryMethod.Network:
						Guard.ArgumentNotNullOrWhiteSpace(_options.Host, nameof(EmailSenderOptions.Host));

						client.DeliveryMethod = SmtpDeliveryMethod.Network;
						client.Host = _options.Host;
						client.Port = _options.Port;
						client.EnableSsl = _options.SecureServerConnection;

						if (!string.IsNullOrWhiteSpace(_options.UserName))
						{
							Guard.ArgumentNotNullOrWhiteSpace(_options.Password, nameof(EmailSenderOptions.Password));

							client.Credentials = new NetworkCredential { UserName = _options.UserName, Password = _options.Password };
						}

						break;
					case SmtpDeliveryMethod.SpecifiedPickupDirectory:
						Guard.ArgumentNotNullOrWhiteSpace(_options.PickupDirectoryLocation, nameof(EmailSenderOptions.PickupDirectoryLocation));
						client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
						client.PickupDirectoryLocation = _options.PickupDirectoryLocation;
						break;
					default:
						throw new NotSupportedException($"Only {nameof(SmtpDeliveryMethod.Network)} and {nameof(SmtpDeliveryMethod.SpecifiedPickupDirectory)} are supported as delivery methods.");
				}

				using var message = new MailMessage(fromAddress?.TrimToLowerInvariant() ?? _options.DefaultFromAddress, email)
				{
					Subject = subject,
					Body = body,
					IsBodyHtml = true
				};

				await client.SendMailAsync(message);
			}
			catch (Exception exc) when (_logger.WriteError(exc, new { email, subject, body, fromAddress }, returnValue: true))
			{
				throw new UmbrellaException("There has been a problem sending the email.", exc);
			}
		}
	}
}