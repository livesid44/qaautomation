using QAAutomation.API.Services;
using Xunit;

namespace QAAutomation.Tests;

/// <summary>
/// Unit tests for <see cref="PiiRedactionService"/>.
/// </summary>
public class PiiRedactionServiceTests
{
    // ── ContainsPii ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Contact me at john.doe@example.com for details.")]
    [InlineData("My number is (555) 867-5309.")]
    [InlineData("SSN: 123-45-6789")]
    [InlineData("Card ending 4111 1111 1111 1111")]
    [InlineData("Server IP is 192.168.0.1")]
    [InlineData("Passport A12345678")]
    [InlineData("DOB: 01/15/1985")]
    [InlineData("ZIP 90210")]
    public void ContainsPii_PiiPresent_ReturnsTrue(string text)
        => Assert.True(PiiRedactionService.ContainsPii(text));

    [Theory]
    [InlineData("The agent greeted the customer warmly.")]
    [InlineData("Julia joined the conversation.")]
    [InlineData("")]
    [InlineData("   ")]
    public void ContainsPii_NoPii_ReturnsFalse(string text)
        => Assert.False(PiiRedactionService.ContainsPii(text));

    // ── DetectTypes ───────────────────────────────────────────────────────────

    [Fact]
    public void DetectTypes_Email_ReturnsEmailLabel()
    {
        var types = PiiRedactionService.DetectTypes("Reach me at alice@contoso.com");
        Assert.Contains("EMAIL", types);
    }

    [Fact]
    public void DetectTypes_Phone_ReturnsPhoneLabel()
    {
        var types = PiiRedactionService.DetectTypes("Call 800-555-1234 for support.");
        Assert.Contains("PHONE", types);
    }

    [Fact]
    public void DetectTypes_Ssn_ReturnsSsnLabel()
    {
        var types = PiiRedactionService.DetectTypes("SSN 987-65-4321");
        Assert.Contains("SSN", types);
    }

    [Fact]
    public void DetectTypes_MultipleTypes_ReturnsAll()
    {
        var text = "Email: bob@example.com  Phone: 555-123-4567";
        var types = PiiRedactionService.DetectTypes(text);
        Assert.Contains("EMAIL", types);
        Assert.Contains("PHONE", types);
    }

    [Fact]
    public void DetectTypes_NoPii_ReturnsEmpty()
        => Assert.Empty(PiiRedactionService.DetectTypes("No PII here whatsoever."));

    // ── Redact ────────────────────────────────────────────────────────────────

    [Fact]
    public void Redact_Email_ReplacedWithPlaceholder()
    {
        var result = PiiRedactionService.Redact("Send to alice@contoso.com please.");
        Assert.DoesNotContain("@", result);
        Assert.Contains("[EMAIL]", result);
    }

    [Fact]
    public void Redact_Phone_ReplacedWithPlaceholder()
    {
        var result = PiiRedactionService.Redact("Call (800) 555-1234.");
        Assert.Contains("[PHONE]", result);
        Assert.DoesNotContain("555-1234", result);
    }

    [Fact]
    public void Redact_Ssn_ReplacedWithPlaceholder()
    {
        var result = PiiRedactionService.Redact("SSN: 123-45-6789");
        Assert.Contains("[SSN]", result);
        Assert.DoesNotContain("123-45-6789", result);
    }

    [Fact]
    public void Redact_IpAddress_ReplacedWithPlaceholder()
    {
        var result = PiiRedactionService.Redact("Server at 10.0.0.1 is down.");
        Assert.Contains("[IP_ADDRESS]", result);
        Assert.DoesNotContain("10.0.0.1", result);
    }

    [Fact]
    public void Redact_NoPii_ReturnsOriginal()
    {
        const string text = "The agent handled the call well.";
        Assert.Equal(text, PiiRedactionService.Redact(text));
    }

    [Fact]
    public void Redact_EmptyString_ReturnsEmpty()
        => Assert.Equal("", PiiRedactionService.Redact(""));

    [Fact]
    public void Redact_MultipleTypes_AllRedacted()
    {
        var result = PiiRedactionService.Redact(
            "Email alice@example.com, phone 555-123-4567, SSN 123-45-6789");
        Assert.Contains("[EMAIL]", result);
        Assert.Contains("[PHONE]", result);
        Assert.Contains("[SSN]", result);
        Assert.DoesNotContain("alice", result);
        Assert.DoesNotContain("555-123-4567", result);
        Assert.DoesNotContain("123-45-6789", result);
    }

    [Fact]
    public void Redact_SampleTranscript_EmailRedacted()
    {
        // Simulates the sample transcript from the problem statement where
        // an email placeholder "(Email)" appears — real emails should be redacted.
        var transcript = "Julia: Is this your best contact email, customer@gmail.com?";
        var result = PiiRedactionService.Redact(transcript);
        Assert.Contains("[EMAIL]", result);
        Assert.DoesNotContain("customer@gmail.com", result);
        // Non-PII content preserved
        Assert.Contains("Julia", result);
        Assert.Contains("best contact email", result);
    }
}
