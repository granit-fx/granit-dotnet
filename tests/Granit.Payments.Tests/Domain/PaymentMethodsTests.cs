using Granit.Payments.Domain;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests.Domain;

public sealed class PaymentMethodsTests
{
    // ======== Card ========

    [Fact]
    public void GetCategory_Card_ShouldReturnCard() =>
        PaymentMethods.GetCategory(PaymentMethods.Card).ShouldBe(PaymentMethodCategory.Card);

    // ======== Bank redirect ========

    [Theory]
    [InlineData(PaymentMethods.Ideal)]
    [InlineData(PaymentMethods.Bancontact)]
    [InlineData(PaymentMethods.Eps)]
    [InlineData(PaymentMethods.Blik)]
    [InlineData(PaymentMethods.Przelewy24)]
    [InlineData(PaymentMethods.Trustly)]
    [InlineData(PaymentMethods.Twint)]
    [InlineData(PaymentMethods.Giropay)]
    [InlineData(PaymentMethods.MyBank)]
    [InlineData(PaymentMethods.Belfius)]
    [InlineData(PaymentMethods.Kbc)]
    public void GetCategory_BankRedirect_ShouldReturnBankRedirect(string methodType) =>
        PaymentMethods.GetCategory(methodType).ShouldBe(PaymentMethodCategory.BankRedirect);

    // ======== Bank transfer ========

    [Fact]
    public void GetCategory_BankTransfer_ShouldReturnBankTransfer() =>
        PaymentMethods.GetCategory(PaymentMethods.BankTransfer).ShouldBe(PaymentMethodCategory.BankTransfer);

    // ======== Bank debit ========

    [Fact]
    public void GetCategory_SepaDebit_ShouldReturnBankDebit() =>
        PaymentMethods.GetCategory(PaymentMethods.SepaDebit).ShouldBe(PaymentMethodCategory.BankDebit);

    // ======== Wallets ========

    [Theory]
    [InlineData(PaymentMethods.ApplePay)]
    [InlineData(PaymentMethods.GooglePay)]
    [InlineData(PaymentMethods.PayPal)]
    public void GetCategory_Wallet_ShouldReturnWallet(string methodType) =>
        PaymentMethods.GetCategory(methodType).ShouldBe(PaymentMethodCategory.Wallet);

    // ======== Buy now pay later ========

    [Theory]
    [InlineData(PaymentMethods.Klarna)]
    [InlineData(PaymentMethods.Alma)]
    [InlineData(PaymentMethods.Riverty)]
    public void GetCategory_BuyNowPayLater_ShouldReturnBuyNowPayLater(string methodType) =>
        PaymentMethods.GetCategory(methodType).ShouldBe(PaymentMethodCategory.BuyNowPayLater);

    // ======== Voucher ========

    [Fact]
    public void GetCategory_Paysafecard_ShouldReturnVoucher() =>
        PaymentMethods.GetCategory(PaymentMethods.Paysafecard).ShouldBe(PaymentMethodCategory.Voucher);

    // ======== Unknown fallback ========

    [Fact]
    public void GetCategory_UnknownMethod_ShouldDefaultToCard() =>
        PaymentMethods.GetCategory("crypto_btc").ShouldBe(PaymentMethodCategory.Card);
}
