using System.Text;
using MimeKit;
using MimeKit.Cryptography;

namespace Granit.TextExtraction.Email.Tests;

internal static class EmlFixtures
{
    public static byte[] PlainTextOnly(
        string subject = "Quarterly figures",
        string body = "Hello team,\nThe Q3 numbers are ready.\nRegards.")
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress("Alice Doe", "alice@example.com"));
        message.To.Add(new MailboxAddress("Bob Smith", "bob@example.com"));
        message.Subject = subject;
        message.Date = new DateTimeOffset(2026, 5, 25, 10, 30, 0, TimeSpan.Zero);
        message.Body = new TextPart("plain") { Text = body };
        return Serialize(message);
    }

    public static byte[] MultipartAlternative(string plainBody, string htmlBody)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "rec@example.com"));
        message.Subject = "Mixed body";
        message.Date = DateTimeOffset.UtcNow;

        Multipart alt = new("alternative")
        {
            new TextPart("plain") { Text = plainBody },
            new TextPart("html") { Text = htmlBody },
        };
        message.Body = alt;
        return Serialize(message);
    }

    public static byte[] HtmlOnly(string htmlBody)
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress("HtmlSender", "html@example.com"));
        message.To.Add(new MailboxAddress("HtmlRecipient", "rec@example.com"));
        message.Subject = "HTML only";
        message.Date = DateTimeOffset.UtcNow;
        message.Body = new TextPart("html") { Text = htmlBody };
        return Serialize(message);
    }

    public static byte[] WithCc()
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("To One", "to1@example.com"));
        message.To.Add(new MailboxAddress("To Two", "to2@example.com"));
        message.Cc.Add(new MailboxAddress("Cc One", "cc1@example.com"));
        message.Cc.Add(new MailboxAddress("Cc Two", "cc2@example.com"));
        message.Subject = "Cc'd";
        message.Date = DateTimeOffset.UtcNow;
        message.Body = new TextPart("plain") { Text = "body" };
        return Serialize(message);
    }

    public static byte[] NestedMultipart()
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "rec@example.com"));
        message.Subject = "Nested";
        message.Date = DateTimeOffset.UtcNow;

        Multipart inner = new("alternative")
        {
            new TextPart("plain") { Text = "nested plain body" },
            new TextPart("html") { Text = "<p>nested html body</p>" },
        };
        Multipart outer = new("mixed")
        {
            inner,
        };
        message.Body = outer;
        return Serialize(message);
    }

    public static byte[] EncodedHeaders()
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress("Élise Müller", "elise@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "rec@example.com"));
        message.Subject = "Café — naïve";
        message.Date = DateTimeOffset.UtcNow;
        message.Body = new TextPart("plain") { Text = "ascii body" };
        return Serialize(message);
    }

    public static byte[] WithAttachment()
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "rec@example.com"));
        message.Subject = "Report attached";
        message.Date = DateTimeOffset.UtcNow;

        Multipart mixed = new("mixed")
        {
            new TextPart("plain") { Text = "See attached." },
        };
        MimePart attachment = new("application", "pdf")
        {
            Content = new MimeContent(new MemoryStream(Encoding.ASCII.GetBytes("PDF-bytes-go-here"))),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            FileName = "report.pdf",
        };
        mixed.Add(attachment);
        message.Body = mixed;
        return Serialize(message);
    }

    public static byte[] EncryptedMultipart()
    {
        MimeMessage message = new();
        message.From.Add(new MailboxAddress("Sender", "sender@example.com"));
        message.To.Add(new MailboxAddress("Recipient", "rec@example.com"));
        message.Subject = "Confidential";
        message.Date = DateTimeOffset.UtcNow;

        // Build a multipart/encrypted body shaped like an OpenPGP-MIME message
        // without any real cryptographic material — the extractor only inspects
        // the wrapper type to decide to short-circuit.
        MultipartEncrypted encrypted = new();
        encrypted.ContentType.Parameters["protocol"] = "application/pgp-encrypted";
        encrypted.Add(new MimePart("application", "pgp-encrypted")
        {
            Content = new MimeContent(new MemoryStream(Encoding.ASCII.GetBytes("Version: 1\n"))),
        });
        encrypted.Add(new MimePart("application", "octet-stream")
        {
            Content = new MimeContent(new MemoryStream(Encoding.ASCII.GetBytes("encrypted-blob"))),
        });
        message.Body = encrypted;
        return Serialize(message);
    }

    public static byte[] Malformed() =>
        // Random bytes that do not parse as a MIME message — neither a valid
        // From/To header nor a body separator. MimeKit raises FormatException.
        Encoding.ASCII.GetBytes("this is not an email\0\0\0nope");

    private static byte[] Serialize(MimeMessage message)
    {
        using MemoryStream ms = new();
        message.WriteTo(ms);
        return ms.ToArray();
    }
}
