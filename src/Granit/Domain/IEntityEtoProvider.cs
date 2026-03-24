namespace Granit.Domain;

/// <summary>
/// Internal bridge between <see cref="IHasEntityEto{TEto}"/> and the
/// <c>EntityLifecycleEventInterceptor</c>.
/// </summary>
/// <remarks>
/// Avoids reflection inside the interceptor hot path: the interceptor casts to this
/// non-generic interface and calls <see cref="GetEto"/> to retrieve both the ETO type
/// and the serialized snapshot without knowing <c>TEto</c> statically.
/// </remarks>
public interface IEntityEtoProvider
{
    /// <summary>
    /// Returns the ETO runtime type and the ETO instance produced by
    /// <see cref="IHasEntityEto{TEto}.ToEto"/>.
    /// </summary>
    (Type EtoType, object Eto) GetEto();
}
