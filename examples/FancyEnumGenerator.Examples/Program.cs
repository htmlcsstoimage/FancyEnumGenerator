Console.WriteLine("--- BasicUsage: Fruit ---");
Console.WriteLine($"Fruit.Apple.ToStringFancy(): {Fruit.Apple.ToStringFancy()}");
Console.WriteLine($"Fruit.TryParseFancy(\"Banana\", ...): {Fruit.TryParseFancy("Banana", out var parsedFruit)}, {parsedFruit}");
Console.WriteLine($"Fruit.Apple.IsUnknown: {Fruit.Apple.IsUnknown}");
Console.WriteLine($"Fruit.Unknown.IsUnknown: {Fruit.Unknown.IsUnknown}");

// Fruit uses the default ValuesType (Span): Values is a ReadOnlySpan over static data in the assembly, so there's no
// allocation and no copy. Being a span, it can't be stored in a field or kept across an await.
var fruitValues = Fruit.Values;
Console.WriteLine($"Fruit.Values.Length: {fruitValues.Length} (a ReadOnlySpan over static data)");
Console.WriteLine($"Fruit.Values contents: {fruitValues[0]}, {fruitValues[1]}, {fruitValues[2]}");

Console.WriteLine();
Console.WriteLine("--- FieldMappings: Beverage ---");
Console.WriteLine($"Beverage.Coffee.Label: {Beverage.Coffee.Label}");
Console.WriteLine($"Beverage.Coffee.SortOrder: {Beverage.Coffee.SortOrder} (value {(int)Beverage.Coffee})");
Console.WriteLine($"Beverage.Tea.SortOrder: {Beverage.Tea.SortOrder} (value {(int)Beverage.Tea}; sorts before Coffee despite the higher value)");
Console.WriteLine($"Beverage.Juice.Label (fallback, NameOfLower): {Beverage.Juice.Label}");
Console.WriteLine($"Beverage.Juice.SortOrder (fallback, NotDefined=-1): {Beverage.Juice.SortOrder}");

// Beverage sets ValuesType = StaticCollection: Values is an IReadOnlyList over a cached array, and AsSpan is a span
// over that same array.
var beverageValues = Beverage.Values;
ReadOnlySpan<Beverage> beverageSpan = Beverage.AsSpan;
Console.WriteLine($"Beverage.AsSpan.Length: {beverageSpan.Length}");
Console.WriteLine($"Beverage.AsSpan contents: {string.Join(", ", beverageSpan.ToArray())}");
Console.WriteLine($"Beverage.AsSpan matches Beverage.Values element-by-element: {beverageSpan[0] == beverageValues[0] && beverageSpan[1] == beverageValues[1] && beverageSpan[2] == beverageValues[2]}");

Console.WriteLine();
Console.WriteLine("--- LargeEnum: HttpHeader (90 members) ---");
Console.WriteLine($"HttpHeader.ContentType.ToStringFancy(): {HttpHeader.ContentType.ToStringFancy()}");
Console.WriteLine($"HttpHeader.TryParseFancy(\"content-type\", ...): {HttpHeader.TryParseFancy("content-type", out var header)}, {header}");
Console.WriteLine($"HttpHeader.TryParseFancy(\"X-FORWARDED-FOR\"u8, ...): {HttpHeader.TryParseFancy("X-FORWARDED-FOR"u8, out var headerFromBytes)}, {headerFromBytes}");
Console.WriteLine($"HttpHeader.UserAgent.NameBytes.Length: {HttpHeader.UserAgent.NameBytes.Length}");

Console.WriteLine();
Console.WriteLine("--- Flags: Permission ---");
var readWrite = Permission.Read | Permission.Write;
Console.WriteLine($"(Read | Write).HasFlagFancy(Read): {readWrite.HasFlagFancy(Permission.Read)}");
Console.WriteLine($"(Read | Write).ToStringFancy(): {readWrite.ToStringFancy()}");
Console.WriteLine($"Permission.All.ToStringFancy() (still works despite ExcludeFromValues): {Permission.All.ToStringFancy()}");

Console.WriteLine();
Console.WriteLine("--- Flags: FlagWithObsolete (LegacySearch = 2 is [Obsolete]) ---");
var searchAndExport = FlagWithObsolete.Search | FlagWithObsolete.Export;
var withLegacy = FlagWithObsolete.Search | (FlagWithObsolete)2; // (FlagWithObsolete)2 is LegacySearch, cast to avoid the obsolete warning
Console.WriteLine($"(Search | Export).ToStringFancy(): {searchAndExport.ToStringFancy()}");
Console.WriteLine($"(Search | Export).IsUnknownAndNotCombined: {searchAndExport.IsUnknownAndNotCombined}");
Console.WriteLine($"LegacySearch.ToStringFancy() (excluded, so empty): '{((FlagWithObsolete)2).ToStringFancy()}'");
Console.WriteLine($"FromUnderlying(2) (LegacySearch's bit is no longer a declared flag): {FlagWithObsolete.FromUnderlying(2)}");
Console.WriteLine($"FromUnderlying(5) (Search | Export): {FlagWithObsolete.FromUnderlying(5).ToStringFancy()}");
Console.WriteLine($"(Search | LegacySearch).IsUnknownAndNotCombined: {withLegacy.IsUnknownAndNotCombined}");

Console.WriteLine();
Console.WriteLine("--- MemberSets: Vegetable ---");
Console.WriteLine($"Vegetable.Carrot.Label: {Vegetable.Carrot.Label}");
Console.WriteLine($"Vegetable.Carrot.SortOrder: {Vegetable.Carrot.SortOrder}");
Console.WriteLine($"Vegetable.Potato.Label (no VegetableMetadata at all): '{Vegetable.Potato.Label}'");
Console.WriteLine($"Vegetable.Potato.SortOrder (DefaultValue fallback): {Vegetable.Potato.SortOrder}");

Console.WriteLine();
Console.WriteLine("--- ConstructorMapping: Widget ---");
Console.WriteLine($"Widget.Sprocket.ClassName (auto-matched ctor param): {Widget.Sprocket.ClassName}");
Console.WriteLine($"Widget.Cog.ClassName (explicit FancyEnumConstructorMapping): {Widget.Cog.ClassName}");
Console.WriteLine($"Widget.Gear.Order (raw ctor arg 10, NOT the transformed 11): {Widget.Gear.Order}");

Console.WriteLine();
Console.WriteLine("--- AssemblyDefaults: Gadget ---");
Console.WriteLine($"Gadget has no Unknown member at all (AllowNoUnknown from assembly default)");
Console.WriteLine($"Gadget.Sprocket.ToStringFancy(): {Gadget.Sprocket.ToStringFancy()}");
