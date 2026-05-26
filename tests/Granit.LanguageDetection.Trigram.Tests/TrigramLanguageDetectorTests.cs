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
        { "pl", "Szybki brązowy lis przeskakuje nad leniwym psem. To zdanie zawiera wszystkie litery polskiego alfabetu i jest często używane do testowania czcionek." },
        { "tr", "Hızlı kahverengi tilki tembel köpeğin üzerinden atlar. Bu cümle Türk alfabesindeki tüm harfleri içerir ve yazı tiplerini test etmek için sıkça kullanılır. İstanbul'un sokaklarında yürürken çocuklar gülerek koşuyor, kuşlar gökyüzünde özgürce uçuyor ve güneş ağaçların arasından parlıyor. Türkçe dilbilgisinde sesli uyumu önemli bir kuraldır." },
        { "sv", "Den snabba bruna räven hoppar över den lata hunden. Denna mening innehåller alla bokstäverna i det svenska alfabetet och används ofta för att testa typsnitt." },
        { "cs", "Rychlá hnědá liška skáče přes líného psa. Tato věta obsahuje všechna písmena české abecedy a často se používá k testování typografických písem." },
        { "zh", "敏捷的棕色狐狸跳过了懒狗。这句话经常用于测试字体，因为它包含许多常见的中文字符和短语结构。" },
        { "ja", "素早い茶色のキツネが怠惰な犬を飛び越える。この文章は日本語のひらがなとカタカナを含み、書体のテストに使われます。" },
        { "ko", "빠른 갈색 여우가 게으른 개를 뛰어넘는다. 이 문장은 한국어의 한글을 사용하여 글꼴을 테스트하기 위해 자주 사용됩니다." },
        { "hi", "भारत एक विशाल देश है जिसकी जनसंख्या एक अरब से अधिक है। यहाँ अनेक भाषाएँ बोली जाती हैं और हिंदी सबसे अधिक बोली जाने वाली भाषा है। दिल्ली भारत की राजधानी है और मुंबई सबसे बड़ा आर्थिक केंद्र माना जाता है। विद्यार्थी स्कूल और विश्वविद्यालय में शिक्षा प्राप्त करते हैं तथा अपने भविष्य का निर्माण करते हैं। सरकार ने अनेक योजनाएँ बनाई हैं जिनके माध्यम से ग्रामीण क्षेत्रों का विकास किया जा रहा है। किसान खेतों में काम करते हैं और फसल उगाते हैं।" },
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
