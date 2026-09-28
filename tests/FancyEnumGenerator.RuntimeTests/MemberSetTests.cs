using System.Linq;
using FancyEnumGenerator.RuntimeTests.Enums;
using Xunit;

namespace FancyEnumGenerator.RuntimeTests;

public class MemberSetTests
{
    [Fact]
    public void ConstructorAndNamedArguments()
    {
        Assert.Equal("carrot", Vegetable.Carrot.Label);
        Assert.Equal(1, Vegetable.Carrot.SortOrder);
        Assert.Equal("pea", Vegetable.Pea.Label);
    }

    [Fact]
    public void ExplicitConstructorMapping() => Assert.Equal("veg-orange", Vegetable.Carrot.ClassName);

    [Fact]
    public void DefaultValueAppliesToMembersWithoutTheProperty()
    {
        Assert.Equal(-1, Vegetable.Pea.SortOrder); // uses the attribute, but not Order
        Assert.Equal(-1, Vegetable.Leek.SortOrder); // doesn't use the attribute at all
    }

    [Fact]
    public void UnmappedStringFieldsAreEmpty()
    {
        Assert.Equal("", Vegetable.Leek.Label);
        Assert.Equal("", Vegetable.Pea.ClassName);
    }

    [Fact]
    public void ParseFromMemberSetField()
    {
        Assert.True(Vegetable.TryParseFrom_Label("pea", out var pea));
        Assert.Equal(Vegetable.Pea, pea);
    }

    [Fact]
    public void InheritedProperty()
    {
        Assert.Equal("root", Vegetable.Carrot.Tag);
        Assert.Equal("", Vegetable.Pea.Tag);
    }

    [Fact]
    public void TypeProperty()
    {
        Assert.Equal(typeof(CarrotHandler), Vegetable.Carrot.Handler);
        Assert.Null(Vegetable.Pea.Handler); // a non-string reference type falls back to null (and is typed nullable)
    }

    [Fact]
    public void ObjectPropertyKeepsTheWrittenType()
    {
        Assert.Equal('x', Assert.IsType<char>(Vegetable.Carrot.Extra));
        Assert.Equal(Shade.Light, Assert.IsType<Shade>(Vegetable.Pea.Extra));
        Assert.Null(Vegetable.Leek.Extra);
    }

    [Fact]
    public void NegativeEnumValue()
    {
        Assert.Equal(Shade.Dark, Vegetable.Carrot.Shade);
        Assert.Equal(default, Vegetable.Pea.Shade);
    }

    [Fact]
    public void IgnoredPropertiesAreNotGenerated() =>
        Assert.DoesNotContain(typeof(VegetableFancyEnumExtensions).GetMethods(), static method => method.Name.Contains("Hidden"));

    [Fact]
    public void RenamedPropertyUsesItsFieldName()
    {
        var names = typeof(VegetableFancyEnumExtensions).GetMethods().Select(static method => method.Name).ToArray();
        Assert.Contains(names, static name => name.Contains("SortOrder"));
        Assert.DoesNotContain(names, static name => name.EndsWith("_Order"));
    }
}
