using Shouldly;
using Xunit;

namespace Granit.LanguageDetection.Trigram.Tests;

public sealed class TrigramLanguageDetectorTests
{
    private static readonly TrigramLanguageDetector Detector = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> KnownSamples => new()
    {
        // Each sample is ~ 1-2 sentences of unambiguous prose for the language.
        { "en", "The quick brown fox jumps over the lazy dog. This sentence is famously used to test typewriters and fonts because it contains every letter of the English alphabet." },
        { "fr", "Le renard brun et rapide saute par-dessus le chien paresseux. Cette phrase contient toutes les lettres de l'alphabet français et sert d'exemple en typographie." },
        { "es", "El veloz murciélago hindú comía feliz cardillo y kiwi. La cigüeña tocaba el saxofón detrás del palenque de paja. Frases típicas para probar fuentes en español." },
        { "de", "Der schnelle braune Fuchs springt über den faulen Hund. Dieser Satz wird häufig verwendet, um Schriftarten zu testen, da er alle Buchstaben des Alphabets enthält." },
        { "it", "La rapida volpe marrone salta sopra il cane pigro. Questa frase contiene tutte le lettere dell'alfabeto italiano ed è usata frequentemente per testare i caratteri tipografici." },
        { "pt", "A rápida raposa marrom salta sobre o cão preguiçoso. Esta frase é frequentemente usada para testar fontes tipográficas porque contém todas as letras do alfabeto português. Conforme as gramáticas brasileiras e portuguesas, esses caracteres especiais ajudam a distinguir o português de idiomas românicos próximos como o galego e o espanhol." },
        { "nl", "De snelle bruine vos springt over de luie hond. Deze zin wordt vaak gebruikt om lettertypen te testen omdat hij alle letters van het Nederlandse alfabet bevat." },
    };

    [Theory]
    [MemberData(nameof(KnownSamples))]
    public async Task Detects_known_language_samples(string expectedIso6391, string sample)
    {
        string? detected = await Detector.DetectAsync(sample, Ct);
        detected.ShouldBe(expectedIso6391);
    }

    [Fact]
    public async Task Detection_is_deterministic_across_runs()
    {
        // Run the same sample 5 times — must return identical answer (no randomness, no
        // wall-clock dependency).
        const string sample = "Le ciel est bleu et les oiseaux chantent. La rivière coule paisiblement à travers la forêt verdoyante.";
        string? first = await Detector.DetectAsync(sample, Ct);
        for (int i = 0; i < 4; i++)
        {
            (await Detector.DetectAsync(sample, Ct)).ShouldBe(first);
        }
    }

    [Fact]
    public async Task Returns_null_on_empty_input()
    {
        string? detected = await Detector.DetectAsync(string.Empty, Ct);
        detected.ShouldBeNull();
    }

    [Fact]
    public async Task Detector_throws_on_null_content()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await Detector.DetectAsync(null!, Ct));
    }

    [Fact]
    public async Task Detector_honours_cancellation()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await Detector.DetectAsync("anything", cts.Token));
    }

    [Fact]
    public void Priority_is_one_hundred() => Detector.Priority.ShouldBe(100);

    [Fact]
    public void Max_sample_chars_defaults_to_2048() => Detector.MaxSampleChars.ShouldBe(2_048);
}
