using Granit.Payments.Domain;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class EfPaymentMethodStore(
    IDbContextFactory<PaymentsDbContext> contextFactory)
    : EfStoreBase<PaymentMethod, PaymentsDbContext>(contextFactory),
      IPaymentMethodReader, IPaymentMethodWriter
{
    public Task<PaymentMethod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<PaymentMethod>> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        ListAsync(Spec.For<PaymentMethod>().Where(m => m.TenantId == tenantId), cancellationToken);

    public Task<PaymentMethod?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IsDefault, cancellationToken);

    Task IPaymentMethodWriter.AddAsync(PaymentMethod method, CancellationToken cancellationToken) =>
        base.AddAsync(method, cancellationToken);

    Task IPaymentMethodWriter.UpdateAsync(PaymentMethod method, CancellationToken cancellationToken) =>
        base.UpdateAsync(method, cancellationToken);

    Task IPaymentMethodWriter.DeleteAsync(PaymentMethod method, CancellationToken cancellationToken) =>
        base.DeleteAsync(method, cancellationToken);
}
