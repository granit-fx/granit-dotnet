namespace Granit.Payments.Domain;

/// <summary>
/// Category of payment method for grouping in the checkout UI.
/// </summary>
public enum PaymentMethodCategory
{
    /// <summary>Credit and debit cards.</summary>
    Card = 0,

    /// <summary>Bank redirect methods (iDEAL, Bancontact, EPS, BLIK, etc.).</summary>
    BankRedirect = 1,

    /// <summary>Bank transfer (SEPA Credit Transfer, wire transfer).</summary>
    BankTransfer = 2,

    /// <summary>Direct debit mandates (SEPA Direct Debit).</summary>
    BankDebit = 3,

    /// <summary>Digital wallets (Apple Pay, Google Pay, PayPal).</summary>
    Wallet = 4,

    /// <summary>Buy now, pay later (Klarna, Alma, Billie, Riverty).</summary>
    BuyNowPayLater = 5,

    /// <summary>Gift cards, vouchers, and prepaid methods.</summary>
    Voucher = 6,

    /// <summary>Point of sale (terminal).</summary>
    PointOfSale = 7,
}

/// <summary>
/// Well-known payment method type identifiers. Extensible — providers can
/// introduce new methods without modifying this class.
/// </summary>
/// <remarks>
/// Method identifiers are lowercase, snake_case strings matching the conventions
/// used by Stripe and Mollie APIs. The checkout UI groups methods by
/// <see cref="PaymentMethodCategory"/>.
/// </remarks>
public static class PaymentMethods
{
    // ── Cards ───────────────────────────────────────────────────────────

    /// <summary>Credit/debit card.</summary>
    public const string Card = "card";

    // ── Bank redirect ──────────────────────────────────────────────────

    /// <summary>iDEAL (Netherlands).</summary>
    public const string Ideal = "ideal";

    /// <summary>Bancontact (Belgium).</summary>
    public const string Bancontact = "bancontact";

    /// <summary>EPS (Austria).</summary>
    public const string Eps = "eps";

    /// <summary>BLIK (Poland).</summary>
    public const string Blik = "blik";

    /// <summary>Przelewy24 (Poland).</summary>
    public const string Przelewy24 = "przelewy24";

    /// <summary>Trustly (Nordics).</summary>
    public const string Trustly = "trustly";

    /// <summary>TWINT (Switzerland).</summary>
    public const string Twint = "twint";

    /// <summary>Giropay (Germany).</summary>
    public const string Giropay = "giropay";

    /// <summary>MyBank (Italy).</summary>
    public const string MyBank = "mybank";

    /// <summary>Belfius Pay Button (Belgium).</summary>
    public const string Belfius = "belfius";

    /// <summary>KBC Payment Button (Belgium).</summary>
    public const string Kbc = "kbc";

    // ── Bank transfer ──────────────────────────────────────────────────

    /// <summary>SEPA bank transfer.</summary>
    public const string BankTransfer = "bank_transfer";

    // ── Bank debit ─────────────────────────────────────────────────────

    /// <summary>SEPA Direct Debit.</summary>
    public const string SepaDebit = "sepa_debit";

    // ── Wallets ────────────────────────────────────────────────────────

    /// <summary>Apple Pay.</summary>
    public const string ApplePay = "apple_pay";

    /// <summary>Google Pay.</summary>
    public const string GooglePay = "google_pay";

    /// <summary>PayPal.</summary>
    public const string PayPal = "paypal";

    // ── Buy Now Pay Later ──────────────────────────────────────────────

    /// <summary>Klarna.</summary>
    public const string Klarna = "klarna";

    /// <summary>Alma.</summary>
    public const string Alma = "alma";

    /// <summary>Riverty (formerly AfterPay).</summary>
    public const string Riverty = "riverty";

    // ── Vouchers ───────────────────────────────────────────────────────

    /// <summary>Paysafecard.</summary>
    public const string Paysafecard = "paysafecard";

    /// <summary>
    /// Returns the category for a well-known method type.
    /// Returns <see cref="PaymentMethodCategory.Card"/> for unknown methods.
    /// </summary>
    public static PaymentMethodCategory GetCategory(string methodType) => methodType switch
    {
        Card => PaymentMethodCategory.Card,
        Ideal or Bancontact or Eps or Blik or Przelewy24 or Trustly
            or Twint or Giropay or MyBank or Belfius or Kbc => PaymentMethodCategory.BankRedirect,
        BankTransfer => PaymentMethodCategory.BankTransfer,
        SepaDebit => PaymentMethodCategory.BankDebit,
        ApplePay or GooglePay or PayPal => PaymentMethodCategory.Wallet,
        Klarna or Alma or Riverty => PaymentMethodCategory.BuyNowPayLater,
        Paysafecard => PaymentMethodCategory.Voucher,
        _ => PaymentMethodCategory.Card,
    };
}
