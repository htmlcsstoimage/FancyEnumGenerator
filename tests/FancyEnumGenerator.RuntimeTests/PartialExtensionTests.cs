using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests.Enums
{
    // The generated classes are partial, so a consumer's own members can live in them: reachable wherever the
    // generated ones are, with no extra using directive. The enum's namespace and accessibility must match.
    public static partial class FruitFancyEnumExtensions
    {
        extension(Fruit fruit)
        {
            public bool IsYellow => fruit == Fruit.Banana;

            // User members can build on the generated ones.
            public string Shout => fruit.ToStringFancy().ToUpperInvariant();
        }

        extension(Fruit)
        {
            public static Fruit Favorite => Fruit.Cherry;
        }

        // The generated constants live in this class too.
        public const int ExtraConstant = FruitFancyEnumExtensions.Length * 10;
    }

    // A consumer's own, non-partial class with the conventional name. FancyEnum generates nothing called FruitExtensions
    // (only FruitFancyEnumExtensions), so this can't clash with generated code.
    public static class FruitExtensions
    {
        public static bool IsApple(this Fruit fruit) => fruit == Fruit.Apple;
    }
}

namespace FancyEnumGenerator.RuntimeTests
{
    public class PartialExtensionTests
    {
        [Fact]
        public void UserMembersSitAlongsideTheGeneratedOnes()
        {
            Assert.True(Fruit.Banana.IsYellow);
            Assert.False(Fruit.Apple.IsYellow);
            Assert.Equal("CHERRY", Fruit.Cherry.Shout);
            Assert.Equal(Fruit.Cherry, Fruit.Favorite);
        }

        [Fact]
        public void UserConstantsCanBuildOnGeneratedOnes() => Assert.Equal(40, FruitFancyEnumExtensions.ExtraConstant);

        [Fact]
        public void AUsersOwnFruitExtensionsClassDoesNotClash() => Assert.True(Fruit.Apple.IsApple());
    }
}
