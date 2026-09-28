Console.WriteLine("--- BasicUsage: Fruit ---");
Console.WriteLine($"Fruit.Apple.ToStringFancy(): {Fruit.Apple.ToStringFancy()}");
Console.WriteLine($"Fruit.TryParseFancy(\"Banana\", ...): {Fruit.TryParseFancy("Banana", out var parsedFruit)}, {parsedFruit}");
Console.WriteLine($"Fruit.Apple.IsUnknown: {Fruit.Apple.IsUnknown}");
Console.WriteLine($"Fruit.Unknown.IsUnknown: {Fruit.Unknown.IsUnknown}");

// Fruit uses the default (CreateStaticReadonlyCollection = false): Values is freshly built on every
// access (cheap - a value-type copy, not a heap allocation) and there is no AsSpan at all, since a
// freshly-built value has no stable backing store for a span to safely point at.
var fruitValues = Fruit.Values;
Console.WriteLine($"Fruit.Values.GetType(): {fruitValues.GetType().Name} (the inline-array struct, freshly built)");
Console.WriteLine($"Fruit.Values contents: {fruitValues[0]}, {fruitValues[1]}, {fruitValues[2]}");

Console.WriteLine();
Console.WriteLine("--- FieldMappings: Beverage ---");
Console.WriteLine($"Beverage.Coffee.Label: {Beverage.Coffee.Label}");
Console.WriteLine($"Beverage.Coffee.SortOrder: {Beverage.Coffee.SortOrder} (value {(int)Beverage.Coffee})");
Console.WriteLine($"Beverage.Tea.SortOrder: {Beverage.Tea.SortOrder} (value {(int)Beverage.Tea}; sorts before Coffee despite the higher value)");
Console.WriteLine($"Beverage.Juice.Label (fallback, NameOfLower): {Beverage.Juice.Label}");
Console.WriteLine($"Beverage.Juice.SortOrder (fallback, NotDefined=-1): {Beverage.Juice.SortOrder}");

// Beverage sets CreateStaticReadonlyCollection = true: Values/AsSpan are backed by the same lazily-built
// static storage. Verify the Unsafe.As/MemoryMarshal reinterpretation is actually correct at runtime
// (matches Values element-by-element), not just that it compiles.
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
